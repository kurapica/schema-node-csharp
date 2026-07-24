using SchemaNode.Event;
using System.Reactive.Subjects;

namespace SchemaNode.UnitTest.App;

/// <summary>
/// Tests for the event system in SchemaNode.App.
/// Tests BaseEvent topic matching and SingleSubject observable behavior.
/// </summary>
[TestClass]
public class EventTest
{
    #region Test Event Classes

    /// <summary>
    /// Simple test event with a fixed topic
    /// </summary>
    private class TestEvent : BaseEvent
    {
        public override string Topic { get; }

        public override string MatchTopic { get; }

        public TestEvent(string topic, string? matchTopic = null)
        {
            Topic = topic;
            MatchTopic = matchTopic ?? "#";
        }
    }

    #endregion

    #region BaseEvent.IsTopicMatch Tests

    [TestMethod]
    public void IsTopicMatch_NullOrWildcardTopic_ReturnsTrue()
    {
        // Empty topic = match all
        var evt = new TestEvent("");
        Assert.IsTrue(evt.IsTopicMatch("anything"));
    }

    [TestMethod]
    public void IsTopicMatch_ExactMatch_ReturnsTrue()
    {
        var evt = new TestEvent("server/topic/action");
        Assert.IsTrue(evt.IsTopicMatch("server/topic/action"));
    }

    [TestMethod]
    public void IsTopicMatch_CaseInsensitive()
    {
        var evt = new TestEvent("Server/Topic/Action");
        Assert.IsTrue(evt.IsTopicMatch("server/topic/action"));
    }

    [TestMethod]
    public void IsTopicMatch_NoMatch_ReturnsFalse()
    {
        var evt = new TestEvent("server/topic/action");
        Assert.IsFalse(evt.IsTopicMatch("server/topic/other"));
    }

    [TestMethod]
    public void IsTopicMatch_SingleWildcard_Plus()
    {
        // "+" matches exactly one segment; remaining unmatched parts are tolerated
        var evt = new TestEvent("server/topic/action");
        Assert.IsTrue(evt.IsTopicMatch("server/+/action"));
        Assert.IsTrue(evt.IsTopicMatch("+/topic/+"));
        // "server/+" matches prefix with + as wildcard for the second segment
        Assert.IsTrue(evt.IsTopicMatch("server/+"));
    }

    [TestMethod]
    public void IsTopicMatch_MultiWildcard()
    {
        var evt = new TestEvent("server/topic/action/guid");
        Assert.IsTrue(evt.IsTopicMatch("server/*"));
        Assert.IsTrue(evt.IsTopicMatch("server/topic/#"));
        Assert.IsTrue(evt.IsTopicMatch("server/topic/*"));
    }

    [TestMethod]
    public void IsTopicMatch_EmptyMatcher_ReturnsFalse()
    {
        var evt = new TestEvent("server/topic/action");
        Assert.IsFalse(evt.IsTopicMatch(""));
    }

    [TestMethod]
    public void IsTopicMatch_EmptyTopic_ReturnsTrue()
    {
        // MatchTopic with empty topic should return true (all contains)
        var evt = new TestEvent("");
        Assert.IsTrue(evt.IsTopicMatch("anything.here"));
    }

    [TestMethod]
    public void IsTopicMatch_StarTopic_ReturnsTrue()
    {
        // "*" in Topic means match all
        var evt = new TestEvent("*");
        Assert.IsTrue(evt.IsTopicMatch("anything"));
    }

    [TestMethod]
    public void IsTopicMatch_LongerTopicThanMatch()
    {
        var evt = new TestEvent("a/b");
        Assert.IsFalse(evt.IsTopicMatch("a/b/c/d"));
    }

    #endregion

    #region SingleSubject Tests

    [TestMethod]
    public void SingleSubject_OnNext_NotifiesAndDisposes()
    {
        using var subject = new SingleSubject<int>();
        int received = 0;

        subject.Subscribe(v => received = v);
        subject.OnNext(42);

        Assert.AreEqual(42, received);
    }

    [TestMethod]
    public void SingleSubject_SecondSubscriber_DoesNotGetPastValue()
    {
        using var subject = new SingleSubject<int>();
        int firstReceived = 0;
        int secondReceived = -1;

        // First subscriber
        subject.Subscribe(v => firstReceived = v);
        subject.OnNext(10);
        Assert.AreEqual(10, firstReceived);

        // Second subscriber should not receive the already-emitted value
        subject.Subscribe(v => secondReceived = v);
        Assert.AreEqual(-1, secondReceived);
    }

    [TestMethod]
    public void SingleSubject_MultipleSubscribers_SameEmission()
    {
        using var subject = new SingleSubject<int>();
        int a = 0, b = 0;

        subject.Subscribe(v => a = v);
        subject.Subscribe(v => b = v);
        subject.OnNext(99);

        Assert.AreEqual(99, a);
        Assert.AreEqual(99, b);
    }

    [TestMethod]
    public void SingleSubject_SecondOnNext_OnlyNotifiesNewSubscribers()
    {
        using var subject = new SingleSubject<int>();
        int firstBatch = 0;
        int secondBatch = 0;

        subject.Subscribe(v => firstBatch = v);
        subject.OnNext(1);
        Assert.AreEqual(1, firstBatch);

        // First subscriber was disposed; new subscriber gets second value
        subject.Subscribe(v => secondBatch = v);
        subject.OnNext(2);

        Assert.AreEqual(1, firstBatch); // unchanged
        Assert.AreEqual(2, secondBatch);
    }

    [TestMethod]
    public void SingleSubject_Dispose_PreventsFurtherEmission()
    {
        var subject = new SingleSubject<int>();
        int received = 0;

        subject.Subscribe(v => received = v);
        subject.Dispose();
        subject.OnNext(42);

        Assert.AreEqual(0, received);
    }

    [TestMethod]
    public void SingleSubject_Disposed_SubscriberGetsCompleted()
    {
        var subject = new SingleSubject<int>();
        subject.Dispose();

        bool completed = false;
        subject.Subscribe(
            _ => { },
            _ => { },
            () => completed = true
        );

        Assert.IsTrue(completed);
    }

    #endregion
}
