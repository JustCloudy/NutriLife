
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class WeightServiceTests
{
    [TestMethod]
    public void Test_AddEntry_SavesCorrectWeight()
    {
        var service = new WeightService();
        var userId = Guid.NewGuid();
        var date = new DateOnly(2026, 11, 22);

        var result = service.AddEntry(userId, date, 80.5m);

        Assert.AreNotEqual(Guid.Empty, result.Id);
        Assert.AreEqual(userId, result.UserId);
        Assert.AreEqual(date, result.Date);
        Assert.AreEqual(80.5m, result.WeightKg);
        Assert.AreNotEqual(default(DateTime), result.CreatedAtUtc);
    }

    [TestMethod]
    public void Test_GetEntries_SortsByDate()
    {
        var service = new WeightService();
        var userId = Guid.NewGuid();

        service.AddEntry(
            userId,
            new DateOnly(2026, 11, 22),
            78.5m);

        service.AddEntry(
            userId,
            new DateOnly(2026, 11, 20),
            80.0m);

        var entries = service.GetEntries(userId);

        Assert.AreEqual(2, entries.Length);
        Assert.AreEqual(80.0m, entries[0].WeightKg);
        Assert.AreEqual(78.5m, entries[1].WeightKg);
    }

    [TestMethod]
    public void Test_GetEntries_NoMeasurements()
    {
        var service = new WeightService();
        var userId = Guid.NewGuid();

        var entries = service.GetEntries(userId);

        Assert.AreEqual(0, entries.Length);
    }

    [TestMethod]
    public void Test_GetEntries_SeparatesUsers()
    {
        var service = new WeightService();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var date = new DateOnly(2026, 11, 22);

        service.AddEntry(user1, date, 80m);
        service.AddEntry(user2, date, 70m);

        var entries = service.GetEntries(user1);

        Assert.AreEqual(1, entries.Length);
        Assert.AreEqual(80m, entries[0].WeightKg);
    }
}
