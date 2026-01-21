using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using NPOI.HSSF.Util;
using SchemaNode.Components;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Http;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Utility;

namespace SchemaNode.Excel;


/// <summary>
/// The schema node excel template manager
/// </summary>
public class TemplateManager
{
    #region Constructor

    /// <summary>
    /// Init the excel template manager
    /// </summary>
    public TemplateManager(SchemaContext context, AppFieldType appField, IFormFile? file = null)
    {
        _context = context;
        _arrayType = appField.SchemaType as ArrayType ?? throw new InvalidCastException("The app field type is not an array type");
        _structType = _arrayType.ElementSchemaType as StructType ?? throw new InvalidCastException("The app field type is not an array type");
        _sheetName = context.GetLocaleString(appField.Display)
                     ?? context.GetLocaleString(_arrayType.Display)
                     ?? context.GetLocaleString(_structType.Display)
                     ?? "Sheet1";

        // write mode
        if (file == null)
        {
            // Prepare the excel
            _workbook = new XSSFWorkbook();
            _sheet = _workbook.CreateSheet(_sheetName);
            _validationHelper = new XSSFDataValidationHelper((XSSFSheet)_sheet);

            IFont headerFont = _workbook.CreateFont();
            headerFont.CloneStyleFrom(_workbook.GetFontAt(0));
            headerFont.IsBold = true;
            headerFont.FontHeightInPoints = 12;

            // number format
            IDataFormat format = _workbook.CreateDataFormat();

            // common border cell style
            _borderCellStyle = _workbook.CreateCellStyle();
            _borderCellStyle.BorderLeft = BorderStyle.Thin;
            _borderCellStyle.BorderRight = BorderStyle.Thin;
            _borderCellStyle.BorderBottom = BorderStyle.Thin;
            _borderCellStyle.BorderTop = BorderStyle.Thin;
            _borderCellStyle.Alignment = HorizontalAlignment.Left;
            _borderCellStyle.VerticalAlignment = VerticalAlignment.Center;

            _pborderCellStyle = _workbook.CreateCellStyle();
            _pborderCellStyle.CloneStyleFrom(_borderCellStyle);
            _pborderCellStyle.FillForegroundColor = PrimaryColor;
            _pborderCellStyle.FillPattern = FillPattern.SolidForeground;

            // header cell style
            _headerCellStyle = _workbook.CreateCellStyle();
            _headerCellStyle.CloneStyleFrom(_borderCellStyle);
            _headerCellStyle.Alignment = HorizontalAlignment.Center;
            _headerCellStyle.SetFont(headerFont);

            _pheaderCellStyle = _workbook.CreateCellStyle();
            _pheaderCellStyle.CloneStyleFrom(_headerCellStyle);
            _pheaderCellStyle.FillForegroundColor = PrimaryColor;
            _pheaderCellStyle.FillPattern = FillPattern.SolidForeground;

            // number cell
            _numberCellStyle = _workbook.CreateCellStyle();
            _numberCellStyle.CloneStyleFrom(_borderCellStyle);
            _numberCellStyle.Alignment = HorizontalAlignment.Right;
            _numberCellStyle.DataFormat = format.GetFormat("General");

            _pnumberCellStyle = _workbook.CreateCellStyle();
            _pnumberCellStyle.CloneStyleFrom(_numberCellStyle);
            _pnumberCellStyle.FillForegroundColor = PrimaryColor;
            _pnumberCellStyle.FillPattern = FillPattern.SolidForeground;

            // date cell style
            _dateCellStyle = _workbook.CreateCellStyle();
            _dateCellStyle.CloneStyleFrom(_borderCellStyle);
            _dateCellStyle.Alignment = HorizontalAlignment.Right;
            _dateCellStyle.DataFormat = format.GetFormat("yyyy/m/d");

            _pdateCellStyle = _workbook.CreateCellStyle();
            _pdateCellStyle.CloneStyleFrom(_dateCellStyle);
            _pdateCellStyle.FillForegroundColor = PrimaryColor;
            _pdateCellStyle.FillPattern = FillPattern.SolidForeground;

            // year month style
            _yearMonthCellStyle = _workbook.CreateCellStyle();
            _yearMonthCellStyle.CloneStyleFrom(_dateCellStyle);
            _yearMonthCellStyle.DataFormat = format.GetFormat("yyyy/m");

            _pyearMonthCellStyle = _workbook.CreateCellStyle();
            _pyearMonthCellStyle.CloneStyleFrom(_yearMonthCellStyle);
            _pyearMonthCellStyle.FillForegroundColor = PrimaryColor;
            _pyearMonthCellStyle.FillPattern = FillPattern.SolidForeground;

            // full date style
            _fullDateCellStyle = _workbook.CreateCellStyle();
            _fullDateCellStyle.CloneStyleFrom(_dateCellStyle);
            _fullDateCellStyle.DataFormat = format.GetFormat("yyyy/m/d h:mm:ss");

            _pfullDateCellStyle = _workbook.CreateCellStyle();
            _pfullDateCellStyle.CloneStyleFrom(_fullDateCellStyle);
            _pfullDateCellStyle.FillForegroundColor = PrimaryColor;
            _pfullDateCellStyle.FillPattern = FillPattern.SolidForeground;

            // int cell style
            _intCellStyle = _workbook.CreateCellStyle();
            _intCellStyle.CloneStyleFrom(_numberCellStyle);
            _intCellStyle.DataFormat = format.GetFormat("0");

            _pintCellStyle = _workbook.CreateCellStyle();
            _pintCellStyle.CloneStyleFrom(_intCellStyle);
            _pintCellStyle.FillForegroundColor = PrimaryColor;
            _pintCellStyle.FillPattern = FillPattern.SolidForeground;
        }
        
        // read mode
        else
        {
            _readMode = true;
            string suffix = Path.GetExtension(file.FileName.ToLower());

            if (suffix.EndsWith("xlsx"))
            {
                _ms = new MemoryStream();
                file.CopyTo(_ms);
                _ms.Seek(0, SeekOrigin.Begin);
                _workbook = new XSSFWorkbook(_ms);
                _sheet = _workbook.GetSheetAt(0);
            }
            else if (suffix.EndsWith("xls"))
            {
                _ms = new MemoryStream();
                file.CopyTo(_ms);
                _ms.Seek(0, SeekOrigin.Begin);
                _workbook = new HSSFWorkbook(_ms);
                _sheet = _workbook.GetSheetAt(0);
            }
            else
            {
                throw new Exception("The file is not a valid excel file");
            }
        }
    }

    #endregion

    #region Method

    /// <summary>
    /// Register enum value for special column field
    /// </summary>
    public void UseEnumForField(string field, List<string> enums) => _enumListMap[field.ToLower()] = enums;
    
    /// <summary>
    /// Register enum entries for special column field
    /// </summary>
    public void UseEnumForField(string field, List<Entry> entries) => _entryListMap[field.ToLower()] = entries;
    
    /// <summary>
    /// Register struct fields for special json field, or it'd be ignored
    /// </summary>
    public void UseStructFieldsForJsonField(string field, List<StructFieldConfig> fields) => _jsonTypeMap[field.ToLower()] = fields;

    #endregion

    #region Render

    /// <summary>
    /// Download excel upload template for the given type
    /// </summary>
    public async Task<SchemaApiFile> DownloadTemplateAsync(int inputRow = 10)
    {
        if (_readMode) throw new Exception("The template manager is in read mode");

        int maxRowHeader = _structType.Fields.Max(f => GetDepth(f.SchemeType));
        int result = 0;

        foreach (var field in _structType.Fields.Where(f => !(f.DisplayOnly ?? false) && !(f.Invisible ?? false)))
        {
            result = await DrawFieldColumns(field, result, 0, maxRowHeader, inputRow,
                isPrimary: (field.Require == true) || _arrayType.Primary != null && _arrayType.Primary.Contains(field.Name));
        }

        // Frozen
        _sheet!.CreateFreezePane(0, maxRowHeader);
        
        // Draw field map, may not existed when upload, so just a backup plan
        var fieldMapSheet = _workbook!.GetSheet(FieldMap);
        int rowIndex = 0;
        foreach (KeyValuePair<int, string> p in _fieldMap)
        {
            IRow enumRow = fieldMapSheet.GetOrCreateRow(rowIndex++);
                        
            ICell enumCell = enumRow.GetOrCreateCell(0);
            enumCell.SetCellValue(p.Key);

            enumCell = enumRow.GetOrCreateCell(1);
            enumCell.SetCellValue(p.Value);
        }

        // generate
        //workbook.GetCreationHelper().CreateFormulaEvaluator().EvaluateAll();
        MemoryStream stream = new();
        _workbook!.Write(stream, true);
        stream.Position = 0;

        // return the api file
        return new SchemaApiFile
        {
            Name = $"{_sheetName}.xlsx",
            Stream = stream
        };
    }

    /// <summary>
    /// Draw field column
    /// </summary>
    async Task<int> DrawFieldColumns(StructFieldConfig field, int startCol, int startRow, int remainRows, int inputRow, string prev = "", string prevDisplay = "", bool isPrimary = false)
    {
        AnySchemaType? nodeType = field.SchemeType;
        if (nodeType is ArrayType arr) nodeType = arr.ElementSchemaType;
        await Task.Yield();

        string token = string.IsNullOrWhiteSpace(prev) ? field.Name.ToLower() : $"{prev}{field.Name.ToLower()}";

        switch (nodeType)
        {
            case EnumType enumType:
            {
                // code
                IRow row = _sheet!.GetOrCreateRow(startRow);
                ICell cell = row.GetOrCreateCell(startCol);
                cell.CellStyle = isPrimary ? _pheaderCellStyle : _headerCellStyle;

                cell.SetCellValue(field.Name);
                _sheet!.SetColumnHidden(startCol, true);
                MergeHeaderCells(startRow, startRow + remainRows - 1, startCol, startCol, isPrimary);

                // register field map
                _fieldMap[startCol] = token;

                // name
                cell = row.GetOrCreateCell(++startCol);
                cell.CellStyle = isPrimary ? _pheaderCellStyle : _headerCellStyle;
                cell.SetCellValue($"{_context.GetLocaleString(field.Display) ?? field.Name}{(!string.IsNullOrWhiteSpace(field.Unit?.Key) ? $"({_context.GetLocaleString(field.Unit)})" : "")}");
                _sheet.SetColumnWidth(startCol, 20 * WidthScale);
                MergeHeaderCells(startRow, startRow + remainRows - 1, startCol, startCol, isPrimary);

                // enum-sheet
                string enumDisplay = (_context.GetLocaleString(enumType.Display) ?? enumType.Name).Replace("/", "-");
                if (_workbook!.GetSheet(enumDisplay) == null)
                {
                    ISheet enumSheet = _workbook.CreateSheet(enumDisplay);
                    List<(string, string)> valueList = new();
                    int enumCascade = field.Cascade ?? (enumType.Cascade is { Length: > 0 } ? enumType.Cascade.Length : 1);
                    if (!string.IsNullOrWhiteSpace(field.Root))
                    {
                        EnumValueAccess[] list = await _context.LoadEnumAccessListAsync(enumType, field.Root, true);
                        enumCascade -= list.Length;
                    }
                    await GenerateEnumList(enumType, valueList,
                        await _context.LoadEnumSubListAsync(enumType, field.Root),
                        field.AnyLevel ?? false,
                        enumCascade,
                        field.WhiteList,
                        field.BlackList);
                    for (int k = 0; k < valueList.Count; k++)
                    {
                        IRow enumRow = enumSheet.GetOrCreateRow(k);
                        
                        ICell enumCell = enumRow.GetOrCreateCell(0);
                        enumCell.SetCellValue(valueList[k].Item2);

                        enumCell = enumRow.GetOrCreateCell(1);
                        enumCell.SetCellValue(valueList[k].Item1);
                    }
                }

                // Example data cell
                startRow += remainRows;
                for (int i = 0; i < inputRow; i++)
                {
                    row = _sheet.GetOrCreateRow(startRow + i);
                    cell = row.GetOrCreateCell(startCol);
                    cell.CellStyle = isPrimary ? _pborderCellStyle : _borderCellStyle;

                    cell = row.GetOrCreateCell(startCol - 1);
                    cell.CellStyle = isPrimary ? _pborderCellStyle : _borderCellStyle;
                    cell.SetCellFormula($"IFERROR(VLOOKUP({ParseCell(startRow + i, startCol)},'{enumDisplay}'!A:B,2,0),\"\")");
                }

                IDataValidation validation = _validationHelper!.CreateValidation(_validationHelper.CreateFormulaListConstraint($"'{enumDisplay}'!$A:$A"), new CellRangeAddressList(startRow, startRow + inputRow - 1, startCol, startCol));
                validation.EmptyCellAllowed = true;
                _sheet.AddValidationData(validation);

                return startCol + 1;
            }
            case ScalarType scalarType:
            {
                // Works like enum type
                if(_entryListMap.TryGetValue(token, out List<Entry>? entries))
                {
                    // code
                    IRow row = _sheet!.GetOrCreateRow(startRow);
                    ICell cell = row.GetOrCreateCell(startCol);
                    cell.CellStyle = isPrimary ? _pheaderCellStyle : _headerCellStyle;

                    cell.SetCellValue(field.Name);
                    _sheet!.SetColumnHidden(startCol, true);
                    MergeHeaderCells(startRow, startRow + remainRows - 1, startCol, startCol, isPrimary);

                    // register field map
                    _fieldMap[startCol] = token;

                    // name
                    cell = row.GetOrCreateCell(++startCol);
                    cell.CellStyle = isPrimary ? _pheaderCellStyle : _headerCellStyle;
                    cell.SetCellValue($"{_context.GetLocaleString(field.Display) ?? field.Name}{(!string.IsNullOrWhiteSpace(field.Unit?.Key) ? $"({_context.GetLocaleString(field.Unit)})" : "")}");
                    _sheet.SetColumnWidth(startCol, 20 * WidthScale);
                    MergeHeaderCells(startRow, startRow + remainRows - 1, startCol, startCol, isPrimary);

                    string sheetName = _context.GetLocaleString(field.Display) ?? field.Name;
                    if (!string.IsNullOrWhiteSpace(prevDisplay)) sheetName = $"{prevDisplay}-{sheetName}";
                    ISheet enumSheet = _workbook!.CreateSheet(sheetName);
                    for (int k = 0; k < entries.Count; k++)
                    {
                        IRow enumRow = enumSheet.GetOrCreateRow(k);
                        ICell enumCell = enumRow.GetOrCreateCell(0);
                        enumCell.SetCellValue(entries[k].Value);
                        
                        enumCell = enumRow.GetOrCreateCell(1);
                        enumCell.SetCellValue(_context.GetLocaleString(entries[k].Label) ?? entries[k].Value);
                    }

                    // Example data cell
                    startRow += remainRows;
                    for (int i = 0; i < inputRow; i++)
                    {
                        row = _sheet.GetOrCreateRow(startRow + i);
                        cell = row.GetOrCreateCell(startCol);
                        cell.CellStyle = isPrimary ? _pborderCellStyle : _borderCellStyle;

                        cell = row.GetOrCreateCell(startCol - 1);
                        cell.CellStyle = isPrimary ? _pborderCellStyle : _borderCellStyle;
                        cell.SetCellFormula($"IFERROR(VLOOKUP({ParseCell(startRow + i, startCol)},'{sheetName}'!A:B,2,0),\"\")");
                    }

                    IDataValidation validation = _validationHelper!.CreateValidation(_validationHelper.CreateFormulaListConstraint($"'{sheetName}'!$A:$A"), new CellRangeAddressList(startRow, startRow + inputRow - 1, startCol, startCol));
                    validation.EmptyCellAllowed = true;
                    _sheet.AddValidationData(validation);

                    return startCol + 1;
                }
                
                // For common scalar type
                else
                {
                    IRow row = _sheet!.GetOrCreateRow(startRow);
                    ICell cell = row.GetOrCreateCell(startCol);
                    cell.CellStyle = isPrimary ? _pheaderCellStyle : _headerCellStyle;
                    cell.SetCellValue(
                        $"{_context.GetLocaleString(field.Display) ?? field.Name}{(!string.IsNullOrWhiteSpace(field.Unit?.Key) ? $"({_context.GetLocaleString(field.Unit)})" : "")}");
                    _sheet!.SetColumnWidth(startCol, 20 * WidthScale);
                    MergeHeaderCells(startRow, startRow + remainRows - 1, startCol, startCol, isPrimary);

                    ICellStyle cellStyle = (isPrimary ? _pborderCellStyle : _borderCellStyle)!;
                    CellRangeAddressList address = new(startRow, startRow + inputRow - 1, startCol, startCol);

                    // register field map
                    _fieldMap[startCol] = token;

                    // Check enum map
                    if (_enumListMap.TryGetValue(token, out List<string>? values))
                    {
                        string sheetName = _context.GetLocaleString(field.Display) ?? field.Name;
                        if (!string.IsNullOrWhiteSpace(prevDisplay)) sheetName = $"{prevDisplay}-{sheetName}";
                        ISheet enumSheet = _workbook!.CreateSheet(sheetName);
                        for (int k = 0; k < values.Count; k++)
                        {
                            IRow enumRow = enumSheet.GetOrCreateRow(k);
                            ICell enumCell = enumRow.GetOrCreateCell(0);
                            enumCell.SetCellValue(values[k]);
                        }

                        IDataValidation validation = _validationHelper!.CreateValidation(
                            _validationHelper.CreateFormulaListConstraint($"'{sheetName}'!$A:$A"), address);
                        validation.EmptyCellAllowed = true;
                        _sheet.AddValidationData(validation);
                    }
                    else
                    {
                        if (scalarType.IsBool)
                        {
                            string sheetName = "_BoolList";
                            if (_workbook!.GetSheet(sheetName) == null)
                            {
                                ISheet enumSheet = _workbook.CreateSheet(sheetName);
                                IRow enumRow = enumSheet.GetOrCreateRow(0);
                                ICell enumCell = enumRow.GetOrCreateCell(0);
                                enumCell.SetCellValue(No);
                                enumRow = enumSheet.GetOrCreateRow(1);
                                enumCell = enumRow.GetOrCreateCell(0);
                                enumCell.SetCellValue(Yes);
                            }

                            IDataValidation validation = _validationHelper!.CreateValidation(
                                _validationHelper.CreateFormulaListConstraint($"'{sheetName}'!$A:$A"), address);
                            validation.EmptyCellAllowed = true;
                            _sheet.AddValidationData(validation);
                        }
                        else if (scalarType.IsDate)
                        {
                            if (scalarType.IsYearMonth)
                            {
                                cellStyle = (isPrimary ? _pyearMonthCellStyle : _yearMonthCellStyle)!;
                            }
                            else if (scalarType.IsFullDate)
                            {
                                cellStyle = (isPrimary ? _pfullDateCellStyle : _fullDateCellStyle)!;
                            }
                            else
                            {
                                cellStyle = (isPrimary ? _pdateCellStyle : _dateCellStyle)!;
                            }
                        }
                        else if (scalarType.IsNumber)
                        {
                            if (scalarType.IsYear)
                            {
                                cellStyle = (isPrimary ? _pintCellStyle : _intCellStyle)!;

                                IDataValidation validation = _validationHelper!.CreateValidation(
                                    _validationHelper.CreateNumericConstraint(ValidationType.INTEGER,
                                        OperatorType.BETWEEN, $"{GetLimit(field.LowLimit, 1970)}",
                                        $"{GetLimit(field.UpLimit, 2099)}"), address);
                                validation.EmptyCellAllowed = true;
                                _sheet.AddValidationData(validation);
                            }
                            else
                            {
                                cellStyle = (scalarType.IsInt
                                    ? (isPrimary ? _pintCellStyle : _intCellStyle)
                                    : (isPrimary ? _pnumberCellStyle : _numberCellStyle))!;
                                if (!string.IsNullOrWhiteSpace(field.LowLimit) &&
                                    !string.IsNullOrWhiteSpace(field.UpLimit))
                                {
                                    IDataValidation validation = _validationHelper!.CreateValidation(
                                        _validationHelper.CreateNumericConstraint(ValidationType.INTEGER,
                                            OperatorType.BETWEEN, $"{GetLimit(field.LowLimit)}",
                                            $"{GetLimit(field.UpLimit)}"), address);
                                    validation.EmptyCellAllowed = true;
                                    _sheet.AddValidationData(validation);
                                }
                                else if (!string.IsNullOrWhiteSpace(field.LowLimit))
                                {
                                    IDataValidation validation = _validationHelper!.CreateValidation(
                                        _validationHelper.CreateNumericConstraint(ValidationType.INTEGER,
                                            OperatorType.GREATER_OR_EQUAL, $"{GetLimit(field.LowLimit)}", ""), address);
                                    validation.EmptyCellAllowed = true;
                                    _sheet.AddValidationData(validation);
                                }
                                else if (!string.IsNullOrWhiteSpace(field.UpLimit))
                                {
                                    IDataValidation validation = _validationHelper!.CreateValidation(
                                        _validationHelper.CreateNumericConstraint(ValidationType.INTEGER,
                                            OperatorType.LESS_OR_EQUAL, $"{GetLimit(field.UpLimit)}", ""), address);
                                    validation.EmptyCellAllowed = true;
                                    _sheet.AddValidationData(validation);
                                }
                            }
                        }
                    }

                    // Example data cell
                    startRow += remainRows;
                    for (int i = 0; i < inputRow; i++)
                    {
                        row = _sheet.GetOrCreateRow(startRow + i);
                        cell = row.GetOrCreateCell(startCol);
                        cell.CellStyle = cellStyle;
                    }

                    return startCol + 1;
                }
            }
            case StructType subStructNode:
                {
                    int beginCol = startCol;
                    string display = _context.GetLocaleString(field.Display) ?? field.Name;
                    if (!string.IsNullOrWhiteSpace(prevDisplay)) display = $"{prevDisplay}-{display}";
                    foreach (var structNodeField in subStructNode.Fields)
                    {
                        startCol = await DrawFieldColumns(structNodeField, startCol, startRow + 1, remainRows - 1, inputRow, $"{token}.",  display, isPrimary);
                    }
                    IRow row = _sheet!.GetOrCreateRow(startRow);
                    ICell cell = row.GetOrCreateCell(beginCol);
                    cell.CellStyle = isPrimary ? _pheaderCellStyle : _headerCellStyle;
                    cell.SetCellValue($"{_context.GetLocaleString(field.Display) ?? field.Name}{(!string.IsNullOrWhiteSpace(field.Unit?.Key) ? $"({_context.GetLocaleString(field.Unit)})" : "")}");

                    MergeHeaderCells(startRow, startRow, beginCol, startCol - 1, isPrimary);
                    return startCol;
                }
            default:
                return startCol;
        }
    }

    /// <summary>
    /// Gets the type depth
    /// </summary>
    private int GetDepth(AnySchemaType? type = null, string? name = null)
    {
        return type switch
        {
            EnumType => 1,
            ScalarType => 1,
            ArrayType => throw new InvalidOperationException("The array element type not supported"),
            StructType @struct => 1 + @struct.Fields.Max(f => GetDepth(f.SchemeType, f.Name)),
            JsonType => !string.IsNullOrEmpty(name) && _jsonTypeMap.TryGetValue(name, out var list) ? list.Max(f => GetDepth(f.SchemeType, f.Name)) : 0,
            _ => 0
        };
    }

    static string GetLimit(string? limit, object? dft = null) => (string.IsNullOrWhiteSpace(limit) && dft != null ? dft.ToString() : limit) ?? "";

    void MergeHeaderCells(int startRow, int endRow, int startCol, int endCol, bool isPrimary)
    {
        if (endRow - startRow <= 0 && endCol - startCol <= 0) return;

        for (int i = startRow; i <= endRow; i++)
        {
            IRow row = _sheet!.GetOrCreateRow(i);
            for (int j = startCol; j <= endCol; j++)
            {
                ICell cell = row.GetOrCreateCell(j);
                cell.CellStyle = isPrimary ? _pheaderCellStyle : _headerCellStyle;
            }
        }

        _sheet!.AddMergedRegion(new CellRangeAddress(startRow, endRow, startCol, endCol));
    }

    async Task GenerateEnumList(EnumType node, List<(string, string)> enumList, EnumValueInfo[] values, bool anyLevel, int level, string[]? whiteList = null, string[]? blackList = null, string prefix = "")
    {
        (string, string, bool)[] items = values.Select(v => (v.Value, _context.GetLocaleString(v.Name)!, v.HasSubList ?? false)).ToArray();
        if (whiteList is { Length: > 0 }) items = items.Where(v => whiteList.Contains(v.Item1)).ToArray();
        if (blackList is { Length: > 0 }) items = items.Where(v => !blackList.Contains(v.Item1)).ToArray();

        level = level - 1;
        if (level > 0)
        {
            foreach ((string, string, bool) item in items)
            {
                string name = string.IsNullOrWhiteSpace(prefix) ? item.Item2 : $"{prefix}/{item.Item2}";
                if (anyLevel || !item.Item3) enumList.Add((item.Item1, name));
                if (item.Item3)
                    await GenerateEnumList(node, enumList, await _context.LoadEnumSubListAsync(node, item.Item1), anyLevel, level, whiteList, blackList, name);
            }
        }
        else
        {
            prefix = string.IsNullOrWhiteSpace(prefix) ? "" : $"{prefix}/";
            enumList.AddRange(items.Select(i => (i.Item1, $"{prefix}{i.Item2}")));
        }
    }

    #endregion

    #region Read Data

    /// <summary>
    /// Read valid data from the excel file
    /// </summary>
    public async Task<JsonArray> ReadUploadsAsync()
    {
        await Task.Yield();
        
        // Try read field map sheet
        var fieldMapSheet = _workbook!.GetSheet(FieldMap);
        if (fieldMapSheet != null)
        {
            _fieldMap.Clear();
            for (int r = 0; r <= fieldMapSheet.LastRowNum; r++)
            {
                IRow row = fieldMapSheet.GetRow(r);
                if (row == null) continue;

                ICell keyCell = row.GetCell(0);
                ICell valueCell = row.GetCell(1);
                if (keyCell is not { CellType: CellType.Numeric } || valueCell == null) continue;
                try
                {
                    // This is only suggest, could be changed by the uploader
                    // But could be use when since locale match may not be available
                    _fieldMap[(int)keyCell.NumericCellValue] = valueCell.GetCellStringValue();
                }
                catch
                {
                    // ignore
                }
            }
        }

        // Read header rows
        int maxRowHeader = _structType.Fields.Max(f => GetDepth(f.SchemeType));
        Dictionary<int, List<StructFieldConfig>> colMap = new();
        _structType.Fields.Where(f => !(f.DisplayOnly ?? false) && !(f.Invisible ?? false)).Aggregate(0, (current, field) 
            => ResolveFieldColumns(field, colMap, current, 0));

        // Scan data
        JsonArray array = [];
        int i = maxRowHeader;
        List<CellRangeAddress> mergedCells = _sheet!.MergedRegions;
        while (i <= _sheet.LastRowNum)
        {
            IRow row = _sheet.GetRow(i);
            if (row == null) continue;

            JsonObject data = new();
            for (int j = 0; j < row.LastCellNum; j++)
            {
                if (colMap.TryGetValue(j, out List<StructFieldConfig>? fields))
                {
                    // Gets the value
                    ICell cell = row.GetCell(j);
                    if (cell == null) continue;
                    string value = cell.GetCellStringValue();

                    // Check the merged cells
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        if (cell.IsMergedCell)
                        {
                            CellRangeAddress? mcell = mergedCells.FirstOrDefault(m => m.IsInRange(cell.RowIndex, cell.ColumnIndex));
                            if (mcell == null) continue;

                            value = _sheet.GetCellStringValue(mcell);
                        }
                        if (string.IsNullOrWhiteSpace(value)) continue;
                    }

                    // Validate by the fields
                    JsonValue? validValue = null;
                    StructFieldConfig last = fields.Last();
                    if (last.SchemeType is EnumType @enum)
                    {
                        switch (@enum.ValueType)
                        {
                            case EnumValueType.String:
                                validValue = JsonValue.Create(value);
                                break;
                            case EnumValueType.Int:
                            case EnumValueType.Flags:
                                if (long.TryParse(value, out long ival))
                                    validValue = JsonValue.Create(ival);
                                break;
                        }
                    }
                    else if (last.SchemeType is ScalarType @scalar)
                    {
                        if (@scalar.IsBool)
                        {
                            validValue = JsonValue.Create(value == Yes);
                        }
                        if (@scalar.IsDate)
                        {
                            if (DateTime.TryParse(value, out DateTime date))
                                validValue = JsonValue.Create(date);
                        }
                        else if (@scalar.IsNumber)
                        {
                            if (@scalar.IsYear || @scalar.IsInt)
                            {
                                if (int.TryParse(value, out int ival))
                                    validValue = JsonValue.Create(ival);
                            }
                            else if (@scalar.IsSingle)
                            {
                                if (float.TryParse(value, out float fval))
                                    validValue = JsonValue.Create(fval);
                            }
                            else
                            {
                                if (decimal.TryParse(value, out decimal dval))
                                    validValue = JsonValue.Create(dval);
                            }
                        }
                        else
                        {
                            validValue = JsonValue.Create(value);
                        }
                    }

                    // save
                    JsonObject container = data;
                    if (validValue != null && !validValue.IsEmpty())
                    {
                        foreach (StructFieldConfig field in fields)
                        {
                            if (field == last)
                            {
                                container[field.Name] = validValue;
                            }
                            else
                            {
                                JsonObject? c = container[field.Name] as JsonObject;
                                if (c is null)
                                {
                                    c = new JsonObject();
                                    container[field.Name] = c;
                                }
                                container = c;
                            }
                        }
                    }
                }
            }

            array.Add(data);

            i++;
        }

        _workbook.Close();
        _ms?.Close();

        // Filter with primary key
        if (_arrayType.Primary is { Length: > 0 })
        {
            JsonArray combineAray = [];
            HashSet<string> map = [];

            foreach (JsonNode? item in array)
            {
                JsonObject? node = item as JsonObject;
                if (node == null || node.IsEmpty()) continue;

                if (_arrayType.Primary.Any(n => node[n].IsEmpty())) continue;
                string key = string.Join('^', _arrayType.Primary.Select(n => node[n]!.ToString()));
                if (map.Add(key))
                    combineAray.Add(node);
            }

            array = combineAray;
        }

        return array;
    }

    /// <summary>
    /// Draw field column
    /// </summary>
    int ResolveFieldColumns(StructFieldConfig field, Dictionary<int, List<StructFieldConfig>> colMap, int startCol, int startRow, List<StructFieldConfig>? fields = null, string prev = "")
    {
        string token = string.IsNullOrWhiteSpace(prev) ? field.Name.ToLower() : $"{prev}{field.Name.ToLower()}";
        switch (field.SchemeType)
        {
            case EnumType:
                {
                    // code
                    IRow row = _sheet!.GetOrCreateRow(startRow);
                    ICell cell = row.GetOrCreateCell(startCol);

                    if (cell.GetCellStringValue().Equals(field.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        colMap[startCol] = CombineFields(fields, field);
                    }

                    return startCol + 2;
                }
            case ScalarType:
                {
                    IRow row = _sheet!.GetOrCreateRow(startRow);
                    ICell cell = row.GetOrCreateCell(startCol);

                    // Entry
                    if (cell.GetCellStringValue().Equals(field.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        colMap[startCol++] = CombineFields(fields, field);
                    }
                    // Common
                    else if (cell.GetCellStringValue().Equals($"{_context.GetLocaleString(field.Display) ?? field.Name}{(!string.IsNullOrWhiteSpace(field.Unit?.Key) ? $"({_context.GetLocaleString(field.Unit)})" : "")}"))
                    {
                        colMap[startCol] = CombineFields(fields, field);
                    }
                    else if (_fieldMap.ContainsValue(token))
                    {
                        startCol = _fieldMap.First(f => f.Value == token).Key;
                        colMap[startCol] = CombineFields(fields, field);
                    }

                    return startCol + 1;
                }
            case StructType subStructType:
                {
                    List<StructFieldConfig> combine = CombineFields(fields, field);
                    return subStructType.Fields.Aggregate(startCol, (current, structNodeField) => ResolveFieldColumns(structNodeField, colMap, current, startRow + 1, combine));
                }
            default:
                return startCol;
        }
    }

    /// <summary>
    /// Combine fields
    /// </summary>
    static List<StructFieldConfig> CombineFields(List<StructFieldConfig>? fields, StructFieldConfig field)
    {
        return fields != null ? [..fields, field] : [field];
    }

    #endregion

    #region Utility

    private const string Yes = "√";
    private const string No = "×";

    private const string ColName = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string FieldMap = "_Field";

    /// <summary>
    /// Convert the col index to col name
    /// </summary>
    /// <param name="col"></param>
    /// <returns></returns>
    static string ParseColName(int col)
    {
        if (col < ColName.Length) return ColName.Substring(col, 1);
        return ColName.Substring(col / ColName.Length - 1, 1) + ColName.Substring(col % ColName.Length, 1);
    }

    /// <summary>
    /// Convert cell index to cell name
    /// </summary>
    static string ParseCell(int row, int col) => $"{ParseColName(col)}{row + 1}";

    private const int WidthScale = 256;
    private const short PrimaryColor = HSSFColor.LightGreen.Index;

    private readonly MemoryStream? _ms;
    private readonly bool _readMode;
    private readonly SchemaContext _context;
    private readonly ArrayType _arrayType;
    private readonly StructType _structType;
    private readonly string _sheetName;
    private readonly IWorkbook? _workbook;
    private readonly ISheet? _sheet;
    private readonly XSSFDataValidationHelper? _validationHelper;
    private readonly ICellStyle? _borderCellStyle;
    private readonly ICellStyle? _headerCellStyle;
    private readonly ICellStyle? _numberCellStyle;
    private readonly ICellStyle? _dateCellStyle;
    private readonly ICellStyle? _yearMonthCellStyle;
    private readonly ICellStyle? _fullDateCellStyle;
    private readonly ICellStyle? _intCellStyle;
    private readonly ICellStyle? _pborderCellStyle;
    private readonly ICellStyle? _pheaderCellStyle;
    private readonly ICellStyle? _pnumberCellStyle;
    private readonly ICellStyle? _pdateCellStyle;
    private readonly ICellStyle? _pyearMonthCellStyle;
    private readonly ICellStyle? _pfullDateCellStyle;
    private readonly ICellStyle? _pintCellStyle;
    private readonly Dictionary<int, string> _fieldMap = [];
    private readonly Dictionary<string, List<string>> _enumListMap = [];
    private readonly Dictionary<string, List<Entry>> _entryListMap = [];
    private readonly Dictionary<string, List<StructFieldConfig>> _jsonTypeMap = [];

    #endregion
}