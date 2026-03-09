import json
import boto3
import os
import io
import logging
from datetime import datetime, timezone

logger = logging.getLogger()
logger.setLevel(logging.INFO)

s3 = boto3.client("s3")
bedrock = boto3.client("bedrock-runtime", region_name="us-east-1")

CONFIG_BUCKET = "summit-takeoff-config"
PROCESSING_BUCKET = "summit-takeoff-processing"
PROMPT_KEY = "extraction_prompt.txt"
REF_TABLE_KEY = "CompRefTable.xlsx"
MODEL_ID = "us.anthropic.claude-sonnet-4-20250514-v1:0"
MAX_TOKENS = 16384


def lambda_handler(event, context):
    try:
        # Determine input format: new (config-based) or legacy (direct image)
        config_path = event.get("config_path")

        if config_path:
            # New format: PDF + client config → crop → extract
            drawing_bucket = event.get("bucket", "summit-takeoff-drawings")
            drawing_key = event["drawing_key"]
            batch_id = event.get("batch_id")

            logger.info(f"Processing drawing: s3://{drawing_bucket}/{drawing_key}")
            logger.info(f"Using config: {config_path}")

            # Load config, prompt, and reference table
            config = load_client_config(config_path)
            prompt_text = load_text_from_s3(CONFIG_BUCKET, PROMPT_KEY)
            ref_table_text = load_reference_table(CONFIG_BUCKET, REF_TABLE_KEY)
            full_prompt = prompt_text + "\n" + ref_table_text

            # Inject BOM column info into prompt
            column_hint = build_column_hint(config)
            if column_hint:
                full_prompt = full_prompt.replace(
                    "## BOM EXTRACTION",
                    f"## BOM EXTRACTION\n\n{column_hint}"
                )

            # Render PDF to full-page image
            render_dpi = config.get("render_dpi", 300)
            drawing_bytes = load_file_from_s3(drawing_bucket, drawing_key)
            page_image = render_pdf_page(drawing_bytes, drawing_key, render_dpi)

            # Crop BOM region(s)
            bom_regions = config.get("bom_regions", [])
            if not bom_regions:
                raise ValueError("Client config has no bom_regions defined")

            bom_crops = []
            for i, region in enumerate(bom_regions):
                crop = crop_region(page_image, region)
                logger.info(f"BOM crop {i+1}: {crop['width']}x{crop['height']} pixels")
                bom_crops.append(crop["image_bytes"])

            # Stitch multi-wrap BOM crops into one tall image
            if len(bom_crops) > 1:
                bom_image = stitch_images(bom_crops)
                logger.info("Stitched multi-wrap BOM crops")
            else:
                bom_image = bom_crops[0]

            # Clamp stitched image to Bedrock's 8000px max dimension limit
            bom_image = clamp_image_dimensions(bom_image, max_dim=8000)

            # Crop title block region(s) - supports multiple sections (e.g., PIPE INFO + Project info)
            # Check for new format (title_block_regions list) first, fall back to old format (title_block_region)
            tb_regions = config.get("title_block_regions", [])
            if not tb_regions:
                old_tb = config.get("title_block_region")
                if old_tb:
                    tb_regions = [old_tb]

            images_for_claude = [{"bytes": bom_image, "type": "image/png", "label": "BOM table"}]

            for i, tb_region in enumerate(tb_regions):
                tb_crop = crop_region(page_image, tb_region)
                label = f"Title block section {i + 1}" if len(tb_regions) > 1 else "Title block"
                logger.info(f"{label} crop: {tb_crop['width']}x{tb_crop['height']} pixels")
                tb_image = clamp_image_dimensions(tb_crop["image_bytes"], max_dim=8000)
                images_for_claude.append({"bytes": tb_image, "type": "image/png", "label": label})

            # Call Bedrock with cropped images
            start_time = datetime.now(timezone.utc)
            raw_response = call_bedrock_multi_image(full_prompt, images_for_claude)
            processing_time_ms = int((datetime.now(timezone.utc) - start_time).total_seconds() * 1000)

        else:
            # Legacy format: direct image file (pre-cropped BOM)
            drawing_bucket, drawing_key = parse_input(event)
            batch_id = None
            logger.info(f"Legacy mode — processing: s3://{drawing_bucket}/{drawing_key}")

            prompt_text = load_text_from_s3(CONFIG_BUCKET, PROMPT_KEY)
            ref_table_text = load_reference_table(CONFIG_BUCKET, REF_TABLE_KEY)
            full_prompt = prompt_text + "\n" + ref_table_text

            image_bytes, media_type = load_drawing_as_image(drawing_bucket, drawing_key)

            start_time = datetime.now(timezone.utc)
            raw_response = call_bedrock(full_prompt, image_bytes, media_type)
            processing_time_ms = int((datetime.now(timezone.utc) - start_time).total_seconds() * 1000)

        # Parse JSON response
        extraction_json = parse_extraction_response(raw_response)
        logger.info(f"Extraction complete: {extraction_json.get('bom_row_count', '?')} BOM items "
                    f"in {processing_time_ms}ms")

        # Write result to processing bucket
        drawing_name = os.path.splitext(os.path.basename(drawing_key))[0]
        if batch_id:
            output_key = f"batches/{batch_id}/extractions/{drawing_name}.json"
            output_body = {
                "source_key": drawing_key,
                "processing_time_ms": processing_time_ms,
                "status": "success",
                "error": None,
                "extraction": extraction_json,
            }
        else:
            output_key = f"extractions/{drawing_name}.json"
            output_body = extraction_json

        s3.put_object(
            Bucket=PROCESSING_BUCKET,
            Key=output_key,
            Body=json.dumps(output_body, indent=2),
            ContentType="application/json",
        )
        logger.info(f"Result written to s3://{PROCESSING_BUCKET}/{output_key}")

        # Return format matches Step Functions expected output
        result = {
            "statusCode": 200,
            "body": extraction_json,
            "output_path": f"s3://{PROCESSING_BUCKET}/{output_key}",
            "processing_time_ms": processing_time_ms,
        }

        if batch_id:
            result["status"] = "success"
            result["source_key"] = drawing_key
            result["output_key"] = output_key
            result["bom_row_count"] = extraction_json.get("bom_row_count", 0)
            result["flagged_count"] = sum(
                1 for item in extraction_json.get("bom_items", [])
                if item.get("confidence") in ("low", "medium")
            )

        return result

    except Exception as e:
        logger.error(f"Extraction failed: {str(e)}", exc_info=True)
        error_result = {
            "statusCode": 500,
            "error": str(e),
        }
        if event.get("batch_id"):
            error_result["status"] = "failed"
            error_result["source_key"] = event.get("drawing_key", "unknown")
        return error_result


# ---------------------------------------------------------------------------
# Config loading
# ---------------------------------------------------------------------------

def load_client_config(config_path):
    """Load client+project config JSON from S3."""
    response = s3.get_object(Bucket=CONFIG_BUCKET, Key=config_path)
    config = json.loads(response["Body"].read().decode("utf-8"))

    # Count title block regions (support both new list format and old single format)
    tb_count = len(config.get("title_block_regions", []))
    if tb_count == 0 and config.get("title_block_region"):
        tb_count = 1

    logger.info(f"Client config loaded: {config.get('client_id')}/{config.get('project_id')}, "
                f"{len(config.get('bom_regions', []))} BOM regions, "
                f"{tb_count} title block regions, "
                f"render_dpi={config.get('render_dpi', 300)}")
    return config


def build_column_hint(config):
    """Build a text hint about BOM column names for this client."""
    columns = config.get("bom_columns", [])
    if not columns:
        return ""

    col_lines = []
    for col in columns:
        label = col.get("label", "")
        role = col.get("role", "")
        col_lines.append(f"- Column labeled \"{label}\" contains the {role}")

    return ("**Client-specific BOM column mapping:**\n"
            "This drawing's BOM table uses the following column names:\n"
            + "\n".join(col_lines) + "\n"
            "Use these column names to identify the correct data for each field.\n")


# ---------------------------------------------------------------------------
# PDF rendering, cropping, and stitching
# ---------------------------------------------------------------------------

def render_pdf_page(pdf_bytes, filename, dpi=300):
    """Render first page of PDF to a PIL Image at the specified DPI."""
    from PIL import Image
    import fitz  # PyMuPDF

    lower = filename.lower()

    # If it's already an image file, just open it directly
    if lower.endswith((".png", ".jpg", ".jpeg")):
        return Image.open(io.BytesIO(pdf_bytes))
    elif lower.endswith((".tif", ".tiff")):
        return Image.open(io.BytesIO(pdf_bytes))
    elif not lower.endswith(".pdf"):
        logger.warning(f"Unknown file type for {filename}, attempting as PDF")

    doc = fitz.open(stream=pdf_bytes, filetype="pdf")
    if doc.page_count == 0:
        raise ValueError("PDF has no pages")
    if doc.page_count > 1:
        logger.warning(f"PDF has {doc.page_count} pages, using page 1 only")

    page = doc[0]
    zoom = dpi / 72
    matrix = fitz.Matrix(zoom, zoom)
    pix = page.get_pixmap(matrix=matrix, alpha=False)

    logger.info(f"PDF rendered: {pix.width}x{pix.height} pixels at {dpi} DPI")

    # Convert to PIL Image for cropping operations
    img = Image.frombytes("RGB", (pix.width, pix.height), pix.samples)
    doc.close()
    return img


def crop_region(page_image, region):
    """Crop a percentage-based region from a PIL Image. Returns PNG bytes and dimensions."""
    page_w, page_h = page_image.size

    x_pct = region["x_pct"]
    y_pct = region["y_pct"]
    w_pct = region["width_pct"]
    h_pct = region["height_pct"]

    left = int(page_w * x_pct / 100)
    top = int(page_h * y_pct / 100)
    right = int(page_w * (x_pct + w_pct) / 100)
    bottom = int(page_h * (y_pct + h_pct) / 100)

    # Clamp to image bounds
    left = max(0, min(left, page_w))
    top = max(0, min(top, page_h))
    right = max(left + 1, min(right, page_w))
    bottom = max(top + 1, min(bottom, page_h))

    cropped = page_image.crop((left, top, right, bottom))

    buf = io.BytesIO()
    cropped.save(buf, format="PNG")
    png_bytes = buf.getvalue()

    logger.info(f"Cropped region ({x_pct},{y_pct},{w_pct},{h_pct})% → "
                f"{cropped.width}x{cropped.height} pixels, {len(png_bytes)} bytes")

    return {
        "image_bytes": png_bytes,
        "width": cropped.width,
        "height": cropped.height,
    }


def stitch_images(image_bytes_list):
    """Vertically stack multiple PNG images into one tall image. Returns PNG bytes."""
    from PIL import Image

    images = [Image.open(io.BytesIO(b)) for b in image_bytes_list]

    total_width = max(img.width for img in images)
    total_height = sum(img.height for img in images)

    stitched = Image.new("RGB", (total_width, total_height), color=(255, 255, 255))

    y_offset = 0
    for img in images:
        stitched.paste(img, (0, y_offset))
        y_offset += img.height

    buf = io.BytesIO()
    stitched.save(buf, format="PNG")
    png_bytes = buf.getvalue()

    logger.info(f"Stitched {len(images)} images → {total_width}x{total_height} pixels, "
                f"{len(png_bytes)} bytes")

    return png_bytes


def clamp_image_dimensions(image_bytes, max_dim=8000):
    """Downscale image proportionally if either dimension exceeds max_dim. Returns PNG bytes."""
    from PIL import Image

    img = Image.open(io.BytesIO(image_bytes))
    w, h = img.size

    if w <= max_dim and h <= max_dim:
        return image_bytes

    scale = min(max_dim / w, max_dim / h)
    new_w = int(w * scale)
    new_h = int(h * scale)
    img = img.resize((new_w, new_h), Image.LANCZOS)

    buf = io.BytesIO()
    img.save(buf, format="PNG")
    png_bytes = buf.getvalue()

    logger.warning(f"Image clamped from {w}x{h} to {new_w}x{new_h} to stay within {max_dim}px Bedrock limit")
    return png_bytes


# ---------------------------------------------------------------------------
# Bedrock API calls
# ---------------------------------------------------------------------------

def call_bedrock_multi_image(system_prompt, images):
    """Call Bedrock with multiple images (BOM crop + title block crops)."""
    content_blocks = []

    for i, img in enumerate(images):
        label = img.get("label", f"Image {i+1}")
        content_blocks.append({
            "text": f"Image {i+1}: {label}",
        })
        content_blocks.append({
            "image": {
                "format": "png",
                "source": {
                    "bytes": img["bytes"],
                },
            },
        })

    # Build instruction text based on number of title block images
    tb_count = len(images) - 1  # First image is always BOM
    if tb_count > 1:
        tb_instruction = (
            f"There are {tb_count} title block section images showing different parts of the title block "
            "(e.g., PIPE INFO section, Project info section). Extract all fields from ALL sections and combine them "
            "into a single unified title_block object. "
        )
    elif tb_count == 1:
        tb_instruction = "The second image is the title block area. "
    else:
        tb_instruction = ""

    content_blocks.append({
        "text": f"Extract the title block metadata and BOM data from these cropped regions of an ISO drawing. "
                f"The first image is the BOM table area. {tb_instruction}"
                "Return the JSON as specified in your instructions.",
    })

    response = bedrock.converse(
        modelId=MODEL_ID,
        messages=[
            {
                "role": "user",
                "content": content_blocks,
            },
        ],
        system=[{"text": system_prompt}],
        inferenceConfig={
            "maxTokens": MAX_TOKENS,
            "temperature": 0,
        },
    )

    output = response.get("output", {})
    message = output.get("message", {})
    content = message.get("content", [])

    text_parts = [block["text"] for block in content if "text" in block]
    if not text_parts:
        raise ValueError("Bedrock returned no text content in response")

    full_text = "\n".join(text_parts)

    usage = response.get("usage", {})
    logger.info(f"Bedrock usage — input tokens: {usage.get('inputTokens', '?')}, "
                f"output tokens: {usage.get('outputTokens', '?')}, "
                f"images sent: {len(images)}")

    stop_reason = response.get("stopReason", "unknown")
    if stop_reason == "max_tokens":
        logger.error("TRUNCATED_RESPONSE: Claude hit max_tokens limit — JSON will be incomplete. "
                     f"Increase MAX_TOKENS (currently {MAX_TOKENS}) or reduce drawing complexity.")
        raise ValueError(f"TRUNCATED_RESPONSE: Bedrock response cut off at {MAX_TOKENS} tokens. "
                         "Increase MAX_TOKENS constant.")

    return full_text


def call_bedrock(full_prompt, image_bytes, media_type):
    """Legacy single-image Bedrock call for backward compatibility."""
    response = bedrock.converse(
        modelId=MODEL_ID,
        messages=[
            {
                "role": "user",
                "content": [
                    {
                        "image": {
                            "format": media_type.split("/")[1],
                            "source": {
                                "bytes": image_bytes,
                            },
                        },
                    },
                    {
                        "text": "Extract the title block metadata and BOM data from this ISO drawing. "
                                "Return the JSON as specified in your instructions.",
                    },
                ],
            },
        ],
        system=[{"text": full_prompt}],
        inferenceConfig={
            "maxTokens": MAX_TOKENS,
            "temperature": 0,
        },
    )

    output = response.get("output", {})
    message = output.get("message", {})
    content = message.get("content", [])

    text_parts = [block["text"] for block in content if "text" in block]
    if not text_parts:
        raise ValueError("Bedrock returned no text content in response")

    full_text = "\n".join(text_parts)

    usage = response.get("usage", {})
    logger.info(f"Bedrock usage — input tokens: {usage.get('inputTokens', '?')}, "
                f"output tokens: {usage.get('outputTokens', '?')}")

    stop_reason = response.get("stopReason", "unknown")
    if stop_reason == "max_tokens":
        logger.error("TRUNCATED_RESPONSE: Claude hit max_tokens limit — JSON will be incomplete. "
                     f"Increase MAX_TOKENS (currently {MAX_TOKENS}) or reduce drawing complexity.")
        raise ValueError(f"TRUNCATED_RESPONSE: Bedrock response cut off at {MAX_TOKENS} tokens. "
                         "Increase MAX_TOKENS constant.")

    return full_text


# ---------------------------------------------------------------------------
# Legacy helper functions
# ---------------------------------------------------------------------------

def parse_input(event):
    """Parse legacy event format (direct bucket/key or s3:// URI)."""
    if "s3_path" in event:
        path = event["s3_path"]
        if not path.startswith("s3://"):
            raise ValueError(f"Invalid s3_path format: {path}. Expected s3://bucket/key")
        parts = path[5:].split("/", 1)
        if len(parts) != 2 or not parts[1]:
            raise ValueError(f"Invalid s3_path format: {path}. Expected s3://bucket/key")
        return parts[0], parts[1]

    bucket = event.get("bucket")
    key = event.get("key")
    if not bucket or not key:
        raise ValueError("Event must contain either 's3_path' or both 'bucket' and 'key'")
    return bucket, key


def load_text_from_s3(bucket, key):
    """Load a text file from S3."""
    response = s3.get_object(Bucket=bucket, Key=key)
    return response["Body"].read().decode("utf-8")


def load_file_from_s3(bucket, key):
    """Load raw file bytes from S3."""
    response = s3.get_object(Bucket=bucket, Key=key)
    return response["Body"].read()


def load_reference_table(bucket, key):
    """Load CompRefTable.xlsx from S3, convert to pipe-delimited text for prompt injection."""
    import openpyxl

    response = s3.get_object(Bucket=bucket, Key=key)
    xlsx_bytes = response["Body"].read()
    wb = openpyxl.load_workbook(io.BytesIO(xlsx_bytes), read_only=True, data_only=True)

    ws = wb["CompRef"]

    lines = []
    for row in ws.iter_rows(min_row=2, values_only=True):
        desc = str(row[0] or "").strip()
        component = str(row[1] or "").strip()
        conn_qty = str(row[2] if row[2] is not None else "").strip()
        conn_type = str(row[3] or "").strip()
        alt_abbr = str(row[4] or "").strip()

        if not desc and not component:
            continue

        lines.append(f"{desc}|{component}|{conn_qty}|{conn_type}|{alt_abbr}")

    wb.close()
    logger.info(f"Reference table loaded: {len(lines)} rows")
    return "\n".join(lines)


def load_drawing_as_image(bucket, key):
    """Legacy: load drawing file and return image bytes + media type."""
    response = s3.get_object(Bucket=bucket, Key=key)
    file_bytes = response["Body"].read()
    lower_key = key.lower()

    if lower_key.endswith(".pdf"):
        return convert_pdf_to_png(file_bytes), "image/png"
    elif lower_key.endswith(".png"):
        return file_bytes, "image/png"
    elif lower_key.endswith((".jpg", ".jpeg")):
        return file_bytes, "image/jpeg"
    elif lower_key.endswith((".tiff", ".tif")):
        return convert_tiff_to_png(file_bytes), "image/png"
    else:
        logger.warning(f"Unknown file extension for {key}, attempting as PNG")
        return file_bytes, "image/png"


def convert_pdf_to_png(pdf_bytes):
    """Legacy: convert PDF to full-page PNG for direct image mode."""
    import fitz

    doc = fitz.open(stream=pdf_bytes, filetype="pdf")
    if doc.page_count == 0:
        raise ValueError("PDF has no pages")
    if doc.page_count > 1:
        logger.warning(f"PDF has {doc.page_count} pages, using page 1 only")

    page = doc[0]
    zoom = 300 / 72
    matrix = fitz.Matrix(zoom, zoom)
    pix = page.get_pixmap(matrix=matrix, alpha=False)
    png_bytes = pix.tobytes("png")
    doc.close()

    logger.info(f"PDF converted to PNG: {pix.width}x{pix.height}, {len(png_bytes)} bytes")

    if len(png_bytes) > 20_000_000:
        logger.warning("PNG exceeds 20MB, retrying at 200 DPI")
        doc = fitz.open(stream=pdf_bytes, filetype="pdf")
        page = doc[0]
        zoom = 200 / 72
        matrix = fitz.Matrix(zoom, zoom)
        pix = page.get_pixmap(matrix=matrix, alpha=False)
        png_bytes = pix.tobytes("png")
        doc.close()
        logger.info(f"Reduced PNG: {pix.width}x{pix.height}, {len(png_bytes)} bytes")

    return png_bytes


def convert_tiff_to_png(tiff_bytes):
    """Convert TIFF to PNG bytes."""
    from PIL import Image

    img = Image.open(io.BytesIO(tiff_bytes))
    buf = io.BytesIO()
    img.save(buf, format="PNG")
    return buf.getvalue()


def parse_extraction_response(raw_text):
    """Parse JSON from Claude's response, stripping markdown fences if present."""
    text = raw_text.strip()

    if text.startswith("```json"):
        text = text[7:]
    elif text.startswith("```"):
        text = text[3:]

    if text.endswith("```"):
        text = text[:-3]

    text = text.strip()

    try:
        result = json.loads(text)
    except json.JSONDecodeError as e:
        logger.error(f"Failed to parse JSON from Claude response: {e}")
        logger.error(f"Raw response (first 2000 chars): {raw_text[:2000]}")
        raise ValueError(f"Claude did not return valid JSON. Parse error: {e}")

    required_keys = ["drawing_number", "title_block", "bom_items", "bom_row_count"]
    missing = [k for k in required_keys if k not in result]
    if missing:
        logger.warning(f"Response JSON missing expected keys: {missing}")

    return result
