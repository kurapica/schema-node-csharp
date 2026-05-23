using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using SchemaNode.Scalar;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Struct;

[Meta<SchemaType>(NS_SYSTEM_RANGE_DATE)]
public class RangeDate
{
    [Meta<SchemaType>(typeof(Date))]
    public DateTimeOffset Start { get;set; }
    
    [Meta<SchemaType>(typeof(Date))]
    public DateTimeOffset End { get;set; }
}

[Meta<SchemaType>(NS_SYSTEM_RANGE_FULL_DATE)]
public class RangeFullDate
{
    [Meta<SchemaType>(typeof(FullDate))]
    public DateTimeOffset Start { get;set; }
    
    [Meta<SchemaType>(typeof(FullDate))]
    public DateTimeOffset End { get;set; }
}

[Meta<SchemaType>(NS_SYSTEM_RANGE_MONTH)]
public class RangeMonth
{
    [Meta<SchemaType>(typeof(YearMonth))]
    public DateTimeOffset Start { get;set; }
    
    [Meta<SchemaType>(typeof(YearMonth))]
    public DateTimeOffset End { get;set; }
}

[Meta<SchemaType>(NS_SYSTEM_RANGE_YEAR)]
public class RangeYear
{
    [Meta<SchemaType>(typeof(Year))]
    public long Start { get;set; }
    
    [Meta<SchemaType>(typeof(Year))]
    public long End { get;set; }
}