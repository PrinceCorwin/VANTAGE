# Rate Sheet Components

All component names recognized by the rate sheet system, drawn from:
- `Resources/RateSheet.json` (EstGrp values)
- `Services/AI/RateSheetService.cs` → `ComponentToEstGrp` (alias map)

## EstGrp values (55) — direct rate sheet keys

```
ANCH       ANLZR      BEV        BU         BW
CLMP       CUT        CVLV       DPAN       EW
EWSH       EWST       FFW        FLSH       FTG
FW         GAUGE      GGLASS     GSKT       HARD
INST       INSTSTND   JPUMP      METER      METERR
OLW        OPRTR      ORFC       PEN        PIPE
PROBE      QCTST      SAD        SAFSHW     SCRD
SHOE       SPEC       SPL        SPRING     SPT
STRESS     STRUT      SW         SWAY       SWGC
THRD       TSPT       TUBE       VLV        VLVOP
VNTDRN     WRAPF      WRAPW      XMTR       XRAY
```

## Alias components → mapped EstGrp

| Alias    | → EstGrp |
|----------|----------|
| BOLT     | HARD     |
| WAS      | HARD     |
| ACT      | OPRTR    |
| FS       | SPT      |
| GRV      | BW       |
| GGLASS   | VLV      |
| METER    | INST     |
| HEAT     | INST     |
| VBF      | VLV      |
| VBFL     | VLV      |
| VBFO     | VLV      |
| VBL      | VLV      |
| VCK      | VLV      |
| VGL      | VLV      |
| VGT      | VLV      |
| VND      | VLV      |
| VPL      | VLV      |
| VPRV     | VLV      |
| VPSV     | VLV      |
| VRLF     | VLV      |
| VSOL     | VLV      |
| VSPL     | VLV      |
| VSW      | VLV      |
| VVNT     | VLV      |
| VYG      | VLV      |
| 45L      | FTG      |
| 90L      | FTG      |
| 90LSR    | FTG      |
| ADPT     | FTG      |
| CAP      | FTG      |
| COV      | FTG      |
| CPLG     | FTG      |
| ELB      | FTG      |
| FLG      | FTG      |
| FLGA     | FTG      |
| FLGB     | FTG      |
| FLGLJ    | FTG      |
| FLGO     | FTG      |
| FLGR     | FTG      |
| F8B      | FTG      |
| FO       | FTG      |
| HOSE     | FTG      |
| LOL      | FTG      |
| NIP      | FTG      |
| PIPET    | FTG      |
| PLG      | FTG      |
| RED      | FTG      |
| REDT     | FTG      |
| SWG      | FTG      |
| SOL      | FTG      |
| STR      | FTG      |
| STUB     | FTG      |
| TEE      | FTG      |
| TOL      | FTG      |
| TRAP     | FTG      |
| UN       | FTG      |
| WOL      | FTG      |

## Combined alphabetical list (all recognized component tokens)

```
45L        90L        90LSR      ACT        ADPT
ANCH       ANLZR      BEV        BOLT       BU
BW         CAP        CLMP       COV        CPLG
CUT        CVLV       DPAN       ELB        EW
EWSH       EWST       F8B        FFW        FLG
FLGA       FLGB       FLGLJ      FLGO       FLGR
FLSH       FO         FS         FTG        FW
GAUGE      GGLASS     GRV        GSKT       HARD
HEAT       HOSE       INST       INSTSTND   JPUMP
LOL        METER      METERR     NIP        OLW
OPRTR      ORFC       PEN        PIPE       PIPET
PLG        PROBE      QCTST      RED        REDT
SAD        SAFSHW     SCRD       SHOE       SOL
SPEC       SPL        SPRING     SPT        STR
STRESS     STRUT      STUB       SW         SWAY
SWG        SWGC       TEE        THRD       TOL
TRAP       TSPT       TUBE       UN         VBF
VBFL       VBFO       VBL        VCK        VGL
VGT        VLV        VLVOP      VND        VNTDRN
VPL        VPRV       VPSV       VRLF       VSOL
VSPL       VSW        VVNT       VYG        WAS
WOL        WRAPF      WRAPW      XMTR       XRAY
```
