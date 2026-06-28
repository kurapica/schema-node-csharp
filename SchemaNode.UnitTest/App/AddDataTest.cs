using System.Text.Json.Nodes;
using SchemaNode.Api.Schema.Application;
using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Property.Common;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.Constant;
using Application = SchemaNode.Property.App.App;
using SchemaType = SchemaNode.Property.Core.SchemaType;
using SchemaNode.Function;
using SchemaNode.Property.App;
using SchemaNode.Schema;
using DataCombine = SchemaNode.Property.App.DataCombine;

namespace SchemaNode.UnitTest.App;


[TestClass]
public class AppDataTest : Base.AppTestBase
{
    #region Application Build
    
    const string APP_NAME = "metting";

    [Meta<Application>(APP_NAME)]
    [Meta<SchemaType>($"meeting.{nameof(Place)}")]
    [Meta<EnableStorage>(true)]
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

    [Meta<Application>(APP_NAME)]
    [Meta<SchemaType>($"meeting.{nameof(Meeting)}")]
    [Meta<EnableStorage>(true)]
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
        [Relation<Default>($"{NS_SYSTEM_DATA}.app.{nameof(SystemAppData.getfield)}", APP_NAME, nameof(Place), nameof(Place.Name), $"${nameof(PlaceId)}")]
        public string? PlaceName { get; set; }

        /// <summary>
        /// The meeting date
        /// </summary>
        public DateTimeOffset Date { get; set; }

        /// <summary>
        /// The total attendance of the meeting
        /// </summary>
        [Meta<DisplayOnly>(true)]
        [Relation<Default>($"{NS_SYSTEM_DATA}.app.{nameof(SystemAppData.getfield)}", APP_NAME, nameof(MeetingCount), nameof(MeetingCount.Count), $"${nameof(Id)}")]
        public long? Count { get; set; }
    }

    [Meta<Application>(APP_NAME)]
    [Meta<SchemaType>($"meeting.{nameof(Attendance)}")]
    [Meta<EnableStorage>(true)]
    public class Attendance
    {
        /// <summary>
        /// The attendance id
        /// </summary>
        [Meta<PrimaryIndex>]
        public Guid AttId { get; set; }
        
        /// <summary>
        /// The meeting id
        /// </summary>
        public Guid MeetId { get; set; }

        /// <summary>
        /// The user name or id
        /// </summary>
        [Meta<UplimitString>(50)]
        public string Name { get; set; } = null!;
        
        /// <summary>
        /// The ticket count used, normally 1
        /// </summary>
        public long Count { get; set; }
    }

    [Meta<Application>(APP_NAME)]
    [Meta<SchemaType>($"meeting.{nameof(MeetingCount)}")]
    [Meta<Push>($"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", nameof(Attendance))]
    [Meta<EnableStorage>(true)]
    public class MeetingCount
    {
        /// <summary>
        /// The meeting id
        /// </summary>
        [Meta<PrimaryIndex>]
        public Guid MeetId { get; set; }

        /// <summary>
        /// The total ticket count of the meeting
        /// </summary>
        [Meta<DataCombine>(DataCombineType.Sum)]
        public long Count { get; set; }
    }
    
    #endregion

    [TestMethod]
    public async Task ComplexPushDataTest()
    {
        string target = Guid.NewGuid().ToString();
        string placeId = Guid.NewGuid().ToString();
        string meetingId = Guid.NewGuid().ToString();
        
        (bool Result, JsonNode? Error) = await Context.PushAppDataAsync(APP_NAME, target, new Dictionary<string, AppDataFieldPushQuery>
        {
            { nameof(Place), new AppDataFieldPushQuery
                {
                    Data = new JsonArray()
                    {
                        new JsonObject
                        {
                            { nameof(Place.Id), placeId },
                            { nameof(Place.Name), "Room A" },
                            { nameof(Place.Capacity), 100 }
                        },
                    }
                }
            },
            { nameof(Meeting), new AppDataFieldPushQuery
                {
                    Data = new JsonArray()
                    {
                        new JsonObject
                        {
                            { nameof(Meeting.Id), meetingId },
                            { nameof(Meeting.Name), "Meeting 1" },
                            { nameof(Meeting.PlaceId), placeId },
                            { nameof(Meeting.Date), DateTimeOffset.UtcNow }
                        }
                    }
                }
            },
            { nameof(Attendance), new AppDataFieldPushQuery
                {
                    Data = new JsonArray()
                    {
                        new JsonObject
                        {
                            { nameof(Attendance.AttId), Guid.NewGuid() },
                            { nameof(Attendance.MeetId), meetingId }, // This should match a Meeting Id
                            { nameof(Attendance.Name), "John Doe" },
                            { nameof(Attendance.Count), 5 }
                        },
                        new JsonObject
                        {
                            { nameof(Attendance.AttId), Guid.NewGuid() },
                            { nameof(Attendance.MeetId), meetingId }, // This should match a Meeting Id
                            { nameof(Attendance.Name), "Bang" },
                            { nameof(Attendance.Count), 3 }
                        },
                        new JsonObject
                        {
                            { nameof(Attendance.AttId), Guid.NewGuid() },
                            { nameof(Attendance.MeetId), meetingId }, // This should match a Meeting Id
                            { nameof(Attendance.Name), "Kind" },
                            { nameof(Attendance.Count), 1 }
                        }
                    }
                }
            }
        });
        
        Assert.IsTrue(Result);
        
        (AppDataResult[] Result, NodeSchema[]? Schemas) result = await Context.BatchQueryAppDataAsync([new AppDataQuery
        {
            App = APP_NAME,
            Target = target,
            Fields = [nameof(Meeting)]
        }]);
        
        Assert.IsTrue(result.Result.Length == 1);
        JsonArray? data =  result.Result[0].Results?[ToCamelCase(nameof(Meeting))] as  JsonArray;
        Assert.IsNotNull(data);
        Assert.AreEqual(1, data.Count);
        Assert.AreEqual("Meeting 1", data[0]![ToCamelCase(nameof(Meeting.Name))]!.GetValue<string>());
        Assert.AreEqual(meetingId, data[0]![ToCamelCase(nameof(Meeting.Id))]!.GetValue<string>());
        Assert.AreEqual("Room A", data[0]![ToCamelCase(nameof(Meeting.PlaceName))]!.GetValue<string>());
        Assert.AreEqual(9, data[0]![ToCamelCase(nameof(Meeting.Count))]!.GetValue<long>());
    }
    
    /// <summary>
    /// Returns the camel case of this string.
    /// </summary>
    internal string ToCamelCase(string value) => value.Length > 0 ? string.Concat(value[..1].ToLowerInvariant(), value.AsSpan(1)) : value;
}
