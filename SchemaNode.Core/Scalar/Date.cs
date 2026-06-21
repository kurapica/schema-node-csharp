using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Scalar;

/// <summary>
/// Represents the date scalar value type, without the hms
/// </summary>
[Meta<ClrEquivalent>(typeof(Date))]
[Meta<ClrEquivalent>(typeof(DateTime))]
[Meta<ClrEquivalent>(typeof(DateOnly))]
[Meta<SchemaType>(NS_SYSTEM_DATE)]
[Meta<OfSchema>(SCHEMA_KIND_DATE)]
public class Date: IScalarType<DateTimeOffset>;

/// <summary>
/// Represents the date scalar value type, with the hms
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_FULL_DATE)]
public class FullDate: Date;

/// <summary>
/// Represents the date scalar value type, ignore the day and hms part
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_YEARMONTH)]
public class YearMonth: Date;