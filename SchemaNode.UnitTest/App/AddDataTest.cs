using SchemaNode.Attribute;
using SchemaNode.Property.Common;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;
using Application = SchemaNode.Property.App.App;
using SchemaType = SchemaNode.Property.Core.SchemaType;
using SchemaNode.Function;

namespace SchemaNode.UnitTest.App;


[TestClass]
public class AppDataTest : Base.AppTestBase
{
    const string APPNAME = "metting";

    [Meta<Application>(APPNAME)]
    [Meta<SchemaType>($"meeting.{nameof(Place)}")]
    public class Place
    {
        /// <summary>
        /// THe place id
        /// </summary>
        [Meta<PrimaryIndex>]
        public Guid Id { get; set; }

        /// <summary>
        /// The place name
        /// </summary>
        [Meta<UplimitString>(50)]
        public string Name { get; set; } = null!;

        /// <summary>
        /// The capacity of the place
        /// </summary>
        [Meta<LowLimitInt>(0)]
        [Meta<UplimitInt>(1000)]
        public long Capacity { get; set; }
    }

    [Meta<Application>(APPNAME)]
    [Meta<SchemaType>($"meeting.{nameof(Meeting)}")]
    public class Meeting
    {
        /// <summary>
        /// The meeting id
        /// </summary>
        [Meta<PrimaryIndex>]
        public Guid Id { get; set; }

        /// <summary>
        /// The meeting name
        /// </summary>
        [Meta<UplimitString>(50)]
        public string Name { get; set; } = null!;

        /// <summary>
        /// The place id of the meeting
        /// </summary>
        public Guid PlaceId { get; set; }

        /// <summary>
        /// The place name
        /// </summary>
        [Meta<DisplayOnly>(true)]
        [Relation<Default>($"{NS_SYSTEM_DATA}.app.{nameof(SystemAppData.getfield)}", APPNAME, nameof(Place), nameof(Place.Name), $"${nameof(PlaceId)}")]
        public string PlaceName { get; set; } = null!;

        /// <summary>
        /// The meeting date
        /// </summary>
        public DateTimeOffset Date { get; set; }

        /// <summary>
        /// The total attendance of the meeting
        /// </summary>
        [Meta<DisplayOnly>(true)]
        [Relation<Default>($"{NS_SYSTEM_DATA}.app.{nameof(SystemAppData.getfield)}", APPNAME, nameof(MeetingCount), nameof(MeetingCount.Total), $"${nameof(Id)}")]
        public long Total { get; set; }
    }

    [Meta<Application>(APPNAME)]
    [Meta<SchemaType>($"meeting.{nameof(Attendance)}")]
    public class Attendance
    {
        /// <summary>
        /// The attendance id
        /// </summary>
        [Meta<PrimaryIndex>]
        public Guid Id { get; set; }

        /// <summary>
        /// The user name or id
        /// </summary>
        [Meta<UplimitString>(50)]
        public string Name { get; set; } = null!;
    }

    [Meta<Application>(APPNAME)]
    [Meta<SchemaType>($"meeting.{nameof(MeetingCount)}")]
    public class MeetingCount
    {
        /// <summary>
        /// The meeting id
        /// </summary>
        [Meta<PrimaryIndex>]
        public Guid Id { get; set; }

        /// <summary>
        /// The total attendance of the meeting
        /// </summary>
        public long Total { get; set; }
    }
}
