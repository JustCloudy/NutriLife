
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class WaterServiceTests
{
    [TestMethod]
    public void Test_AddEntry_SavesCorrectData()
    {
        var service = new WaterService();
        var userId = Guid.NewGuid();
        var date = new DateOnly(2026, 11, 22);

        var result = service.AddEntry(userId, date, 250);

        Assert.AreNotEqual(Guid.Empty, result.Id);
        Assert.AreEqual(userId, result.UserId);
        Assert.AreEqual(date, result.Date);
        Assert.AreEqual(250, result.AmountMl);
    }

    [TestMethod]
    public void Test_GetEntries_CalculatesDailyTotal()
    {
        var service = new WaterService();
        var userId = Guid.NewGuid();
        var date = new DateOnly(2026, 11, 22);

        service.AddEntry(userId, date, 250);
        service.AddEntry(userId, date, 500);
        service.AddEntry(userId, date.AddDays(-1), 900);

        var entries = service.GetEntries(userId, date);

        Assert.AreEqual(2, entries.Length);
        Assert.AreEqual(750, entries.Sum(e => e.AmountMl));
    }

    [TestMethod]
    public void Test_GetEntries_NoEntries()
    {
        var service = new WaterService();
        var userId = Guid.NewGuid();
        var date = new DateOnly(2026, 11, 22);

        var entries = service.GetEntries(userId, date);

        Assert.AreEqual(0, entries.Length);
    }

    [TestMethod]
    public void Test_GetEntries_SeparatesUsers()
    {
        var service = new WaterService();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var date = new DateOnly(2026, 11, 22);

        service.AddEntry(user1, date, 250);
        service.AddEntry(user2, date, 500);

        var entries = service.GetEntries(user1, date);

        Assert.AreEqual(1, entries.Length);
        Assert.AreEqual(250, entries[0].AmountMl);
    }
}
