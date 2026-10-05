ROUTINE EDList [Type=INC]

#define ListColumnTypeText     "TEXT"
#define ListColumnTypeNumber   "NUMBER"
#define ListColumnTypeDate     "DATE"
#define ListColumnTypeDateTime "DATETIME"
#define ListColumnTypeBool     "BOOL"
#define ListColumnTypeImage    "IMAGE"
#define ListColumnTypeColor    "COLOR"
#define ListColumnTypeHHMM     "HHMM"
#define ListColumnTypeCodebook "CODEBOOK"
#define ListColumnTypes        $lb($$$ListColumnTypeText,$$$ListColumnTypeNumber,$$$ListColumnTypeDate,$$$ListColumnTypeDateTime,$$$ListColumnTypeBool,$$$ListColumnTypeImage,$$$ListColumnTypeColor,$$$ListColumnTypeHHMM,$$$ListColumnTypeCodebook)

#define ListAlignLeft   "LEFT"
#define ListAlignCenter "CENTER"
#define ListAlignRight  "RIGHT"
#define ListAligns      $lb($$$ListAlignLeft,$$$ListAlignCenter,$$$ListAlignRight)
