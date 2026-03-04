import json
import boto3
import io
import logging
from datetime import datetime, timezone
from openpyxl import Workbook
from openpyxl.styles import Font, PatternFill, Border, Side
from openpyxl.utils import get_column_letter

logger = logging.getLogger()
logger.setLevel(logging.INFO)

s3 = boto3.client("s3")

PROCESSING_BUCKET = "summit-takeoff-processing"


def lambda_handler(event, context):
    """
    Aggregation Lambda — combines per-drawing extraction JSONs into Excel output.
    Outputs 2 tabs: Material (all BOM items), Flagged (low/medium confidence items).
    All business logic (Labor generation, summary, rates) handled by app.
    """
    try:
        start_time = datetime.now(timezone.utc)
        
        if "extraction_keys" in event:
            extraction_keys = event["extraction_keys"]
            batch_id = event.get("batch_id", f"test-{start_time.strftime('%Y%m%d-%H%M%S')}")
            logger.info(f"Test mode: {len(extraction_keys)} extraction keys provided")
        elif "batch_id" in event:
            batch_id = event["batch_id"]
            extraction_keys = load_batch_extraction_keys(batch_id)
            logger.info(f"Batch mode: {len(extraction_keys)} extractions found for {batch_id}")
        else:
            raise ValueError("Event must contain either 'extraction_keys' or 'batch_id'")
        
        extractions = []
        for key in extraction_keys:
            extraction = load_extraction(key)
            if extraction:
                extractions.append(extraction)
        
        logger.info(f"Loaded {len(extractions)} successful extractions")
        
        if not extractions:
            raise ValueError("No valid extractions to aggregate")
        
        material_rows = build_material_rows(extractions)
        flagged_rows = build_flagged_rows(extractions)
        
        logger.info(f"Built {len(material_rows)} material rows, {len(flagged_rows)} flagged rows")
        
        excel_bytes = generate_excel(material_rows, flagged_rows)
        
        excel_key = f"batches/{batch_id}/output/takeoff_{batch_id}.xlsx"
        s3.put_object(
            Bucket=PROCESSING_BUCKET,
            Key=excel_key,
            Body=excel_bytes,
            ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        )
        logger.info(f"Excel written to s3://{PROCESSING_BUCKET}/{excel_key}")
        
        elapsed_ms = int((datetime.now(timezone.utc) - start_time).total_seconds() * 1000)
        
        return {
            "status": "completed",
            "batch_id": batch_id,
            "excel_path": excel_key,
            "total_drawings": len(extractions),
            "total_material_rows": len(material_rows),
            "total_flagged_rows": len(flagged_rows),
            "processing_time_ms": elapsed_ms
        }
        
    except Exception as e:
        logger.error(f"Aggregation failed: {str(e)}", exc_info=True)
        return {
            "status": "failed",
            "error": str(e)
        }


def load_batch_extraction_keys(batch_id):
    prefix = f"batches/{batch_id}/extractions/"
    paginator = s3.get_paginator("list_objects_v2")
    keys = []
    
    for page in paginator.paginate(Bucket=PROCESSING_BUCKET, Prefix=prefix):
        for obj in page.get("Contents", []):
            key = obj["Key"]
            if key.endswith(".json"):
                keys.append(key)
    
    return keys


def load_extraction(key):
    try:
        response = s3.get_object(Bucket=PROCESSING_BUCKET, Key=key)
        data = json.loads(response["Body"].read().decode("utf-8"))
        
        if "extraction" in data and "status" in data:
            if data["status"] != "success":
                logger.warning(f"Skipping failed extraction: {key}")
                return None
            extraction = data["extraction"]
            extraction["_source_key"] = data.get("source_key", key)
        else:
            extraction = data
            extraction["_source_key"] = key
        
        return extraction
        
    except Exception as e:
        logger.error(f"Failed to load extraction {key}: {e}")
        return None


def clean_quantity(qty):
    """
    Convert quantity to decimal number, stripping ' and " marks.
    "41.3'" -> 41.3
    "12" -> 12
    "5.5\"" -> 5.5
    """
    if qty is None:
        return None
    
    qty_str = str(qty).replace("'", "").replace('"', "").strip()
    
    try:
        return float(qty_str)
    except (ValueError, TypeError):
        return qty  # Return original if can't parse


def build_material_rows(extractions):
    """
    Build material rows — one row per BOM item.
    ShopField: 1 = shop, 2 = field
    Rule: BU connections OR zero-connection items = field (2), else shop (1)
    Includes both concatenated description and raw_description.
    """
    rows = []
    
    for extraction in extractions:
        drawing_number = extraction.get("drawing_number", "UNKNOWN")
        title_block = extraction.get("title_block", {})
        bom_items = extraction.get("bom_items", [])
        
        # Get pipe spec from title block
        pipe_spec = (
            title_block.get("Pipe Spec") or
            title_block.get("PIPE SPEC") or
            title_block.get("Piping Spec") or
            title_block.get("PIPING SPEC") or
            title_block.get("Spec") or
            title_block.get("SPEC") or
            ""
        )
        
        for item in bom_items:
            conn_type = item.get("connection_type") or ""
            conn_qty = item.get("connection_qty", 0) or 0
            
            if conn_qty == 0:
                shop_field = 2
            elif "BU" in str(conn_type).upper():
                shop_field = 2
            else:
                shop_field = 1
            
            # Build concatenated description
            # Format: size IN - component - thickness - class - pipe spec - material - length
            size = item.get("size") or ""
            thickness = item.get("thickness") or ""
            material = item.get("material") or ""
            component = item.get("component") or ""
            commodity_code = item.get("commodity_code") or ""
            class_rating = item.get("class_rating") or ""
            length = item.get("length") or ""
            
            desc_parts = []
            if size:
                desc_parts.append(f"{size} IN")
            if component:
                desc_parts.append(component)
            if thickness:
                desc_parts.append(thickness)
            if class_rating:
                desc_parts.append(class_rating)
            if pipe_spec:
                desc_parts.append(pipe_spec)
            if material:
                desc_parts.append(material)
            if length:
                desc_parts.append(length)
            
            concat_description = " - ".join(desc_parts)
            
            row = {
                "drawing_number": drawing_number,
                "item_id": item.get("item_id"),
                "component": component,
                "size": size,
                "description": concat_description,
                "raw_description": item.get("description"),
                "quantity": clean_quantity(item.get("quantity")),
                "connection_qty": conn_qty,
                "connection_type": conn_type,
                "connection_size": item.get("connection_size"),
                "thickness": thickness,
                "class_rating": item.get("class_rating"),
                "length": item.get("length"),
                "material": material,
                "commodity_code": commodity_code,
                "shop_field": shop_field,
                "confidence": item.get("confidence"),
                "flag": item.get("flag"),
                **{f"tb_{k}": v for k, v in title_block.items()}
            }
            rows.append(row)
    
    return rows


def build_flagged_rows(extractions):
    rows = []
    
    for extraction in extractions:
        drawing_number = extraction.get("drawing_number", "UNKNOWN")
        title_block = extraction.get("title_block", {})
        bom_items = extraction.get("bom_items", [])
        
        for item in bom_items:
            confidence = item.get("confidence", "high")
            
            if confidence.lower() not in ("low", "medium"):
                continue
            
            row = {
                "drawing_number": drawing_number,
                "item_id": item.get("item_id"),
                "size": item.get("size"),
                "description": item.get("description"),
                "component": item.get("component"),
                "connection_qty": item.get("connection_qty"),
                "connection_type": item.get("connection_type"),
                "material": item.get("material"),
                "thickness": item.get("thickness"),
                "class_rating": item.get("class_rating"),
                "confidence": confidence,
                "flag": item.get("flag"),
                "override_component": "",
                "override_connection_qty": "",
                "override_connection_type": "",
                "override_notes": "",
                "pipe_spec": (
                    title_block.get("Pipe Spec") or
                    title_block.get("PIPE SPEC") or
                    title_block.get("Piping Spec") or
                    ""
                )
            }
            rows.append(row)
    
    return rows


def generate_excel(material_rows, flagged_rows):
    wb = Workbook()
    
    header_font = Font(bold=True)
    header_fill = PatternFill(start_color="DAEEF3", end_color="DAEEF3", fill_type="solid")
    thin_border = Border(
        left=Side(style="thin"),
        right=Side(style="thin"),
        top=Side(style="thin"),
        bottom=Side(style="thin")
    )
    
    ws_material = wb.active
    ws_material.title = "Material"
    write_material_tab(ws_material, material_rows, header_font, header_fill, thin_border)
    
    ws_flagged = wb.create_sheet("Flagged")
    write_flagged_tab(ws_flagged, flagged_rows, header_font, header_fill, thin_border)
    
    buffer = io.BytesIO()
    wb.save(buffer)
    buffer.seek(0)
    return buffer.getvalue()


def write_material_tab(ws, material_rows, header_font, header_fill, thin_border):
    if not material_rows:
        ws.cell(row=1, column=1, value="No material data")
        return
    
    tb_fields = set()
    for row in material_rows:
        for key in row.keys():
            if key.startswith("tb_"):
                tb_fields.add(key)
    tb_fields = sorted(list(tb_fields))
    
    fixed_columns = [
        ("drawing_number", "Drawing Number"),
        ("item_id", "Item ID"),
        ("component", "Component"),
        ("size", "Size"),
        ("description", "Description"),
        ("raw_description", "Raw Description"),
        ("quantity", "Quantity"),
        ("connection_qty", "Connection Qty"),
        ("connection_type", "Connection Type"),
        ("connection_size", "Connection Size"),
        ("thickness", "Thickness"),
        ("class_rating", "Class Rating"),
        ("length", "Length"),
        ("material", "Material"),
        ("commodity_code", "Commodity Code"),
        ("shop_field", "ShopField"),
        ("confidence", "Confidence"),
        ("flag", "Flag"),
    ]
    
    columns = fixed_columns + [(tb, tb.replace("tb_", "")) for tb in tb_fields]
    
    for col_idx, (key, label) in enumerate(columns, start=1):
        cell = ws.cell(row=1, column=col_idx, value=label)
        cell.font = header_font
        cell.fill = header_fill
        cell.border = thin_border
    
    for row_idx, row_data in enumerate(material_rows, start=2):
        for col_idx, (key, label) in enumerate(columns, start=1):
            value = row_data.get(key, "")
            cell = ws.cell(row=row_idx, column=col_idx, value=value)
            cell.border = thin_border
    
    for col_idx, (key, label) in enumerate(columns, start=1):
        max_len = len(label)
        for row_data in material_rows[:100]:
            val_len = len(str(row_data.get(key, "")))
            if val_len > max_len:
                max_len = val_len
        width = min(max(max_len + 2, 10), 50)
        ws.column_dimensions[get_column_letter(col_idx)].width = width
    
    ws.freeze_panes = "A2"


def write_flagged_tab(ws, flagged_rows, header_font, header_fill, thin_border):
    if not flagged_rows:
        ws.cell(row=1, column=1, value="No flagged items — all extractions high confidence")
        return
    
    columns = [
        ("drawing_number", "Drawing Number"),
        ("item_id", "Item ID"),
        ("size", "Size"),
        ("description", "Description"),
        ("component", "Component"),
        ("connection_qty", "Connection Qty"),
        ("connection_type", "Connection Type"),
        ("material", "Material"),
        ("thickness", "Thickness"),
        ("class_rating", "Class Rating"),
        ("pipe_spec", "Pipe Spec"),
        ("confidence", "Confidence"),
        ("flag", "Flag Reason"),
        ("override_component", "Override Component"),
        ("override_connection_qty", "Override Conn Qty"),
        ("override_connection_type", "Override Conn Type"),
        ("override_notes", "Override Notes"),
    ]
    
    override_fill = PatternFill(start_color="FFFFCC", end_color="FFFFCC", fill_type="solid")
    
    for col_idx, (key, label) in enumerate(columns, start=1):
        cell = ws.cell(row=1, column=col_idx, value=label)
        cell.font = header_font
        cell.fill = header_fill if not key.startswith("override_") else override_fill
        cell.border = thin_border
    
    for row_idx, row_data in enumerate(flagged_rows, start=2):
        for col_idx, (key, label) in enumerate(columns, start=1):
            value = row_data.get(key, "")
            cell = ws.cell(row=row_idx, column=col_idx, value=value)
            cell.border = thin_border
            if key.startswith("override_"):
                cell.fill = override_fill
    
    for col_idx, (key, label) in enumerate(columns, start=1):
        max_len = len(label)
        for row_data in flagged_rows[:100]:
            val_len = len(str(row_data.get(key, "")))
            if val_len > max_len:
                max_len = val_len
        width = min(max(max_len + 2, 10), 50)
        ws.column_dimensions[get_column_letter(col_idx)].width = width
    
    ws.freeze_panes = "A2"
