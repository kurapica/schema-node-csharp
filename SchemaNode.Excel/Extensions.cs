using NPOI.SS.UserModel;
using NPOI.SS.Util;
using System.Globalization;
using System.Text.Json.Nodes;

namespace SchemaNode.Excel;

internal static class Extensions
{
    #region Npoi
    
    /// <summary>
    /// Get or create the row
    /// </summary>
    internal static IRow GetOrCreateRow(this ISheet sheet, int rowIndex) => sheet.GetRow(rowIndex) ?? sheet.CreateRow(rowIndex);

    /// <summary>
    /// Get or create the cell
    /// </summary>
    internal static ICell GetOrCreateCell(this IRow row, int cellIndex) => row.GetCell(cellIndex) ?? row.CreateCell(cellIndex);

    // Check if a cell is null
    internal static bool IsCellNullOrEmpty(this ISheet sheet, int rowIndex, int cellIndex)
    {
        IRow row = sheet.GetRow(rowIndex);
        if (row == null) return true;
        ICell cell = row.GetCell(cellIndex);
        return cell == null || cell.IsNullOrEmpty();
    }

    // Check a cell contains null or empty value
    internal static bool IsNullOrEmpty(this ICell? cell)
    {
        if (cell == null) return true;

        switch (cell.CellType)
        {
            case CellType.String:
                return string.IsNullOrWhiteSpace(cell.StringCellValue);
            case CellType.Numeric:
            case CellType.Boolean:
            case CellType.Formula:
            case CellType.Error:
                return false;
        }

        // null, blank or unknown
        return true;
    }

    /// <summary>
    /// Gets the cell value
    /// </summary>
    internal static object? GetCellValue(this ICell? cell)
    {
        if (cell == null) return null;

        switch (cell.CellType)
        {
            case CellType.Numeric:
                if (DateUtil.IsCellDateFormatted(cell))
                {
                    DateTime? date = cell.DateCellValue;
                    return date switch
                    {
                        null => null,
                        { Hour: 0, Minute: 0, Second: 0 } => date.Value.ToString("yyyy-MM-dd"),
                        _ => date
                    };
                }
                else
                {
                    return cell.NumericCellValue;
                }
            case CellType.String:
                return cell.StringCellValue;
            default:
                return null;
        }
    }

    /// <summary>
    /// Get cell string value
    /// </summary>
    internal static string GetCellStringValue(this ICell? cell)
    {
        if (cell == null) return "";

        switch (cell.CellType)
        {
            case CellType.Numeric:
                if (DateUtil.IsCellDateFormatted(cell))
                {
                    DateTime? date = cell.DateCellValue;
                    return date switch
                    {
                        null => "",
                        { Hour: 0, Minute: 0, Second: 0 } => date.Value.ToString("yyyy-MM-dd"),
                        _ => date.Value.ToString("yyyy-MM-dd hh:mm:ss")
                    };
                }
                else
                {
                    return cell.NumericCellValue.ToString(CultureInfo.InvariantCulture);
                }
            default:
                return cell.StringCellValue;
        }
    }

    internal static string GetCellStringValue(this ISheet sheet, CellRangeAddress mergedCell)
    {
        for (int i = mergedCell.FirstRow; i <= mergedCell.LastRow; i++)
        {
            IRow row = sheet.GetRow(i);
            if (row == null) continue;
            for (int j = mergedCell.FirstColumn; j <= mergedCell.LastColumn; j++)
            {
                ICell cell = row.GetCell(j);
                if (cell == null) continue;
                string value = cell.GetCellStringValue();
                if(!string.IsNullOrWhiteSpace(value)) return value;
            }
        }
        return "";
    }
    
    #endregion
}

