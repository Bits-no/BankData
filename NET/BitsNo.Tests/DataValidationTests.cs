namespace BitsNo.Tests;

public class DataValidationTests
{
    [TestCase(0)]
    [TestCase(999)]
    [TestCase(1001)]
    [TestCase(1099)]
    [TestCase(9999)]
    [TestCase(10000)]
    public void BankDataNotFoundTest(int clearing) =>
        Assert.That(Data.Banks.GetBankFromClearing(clearing), Is.Null);

    [Test]
    public void DumpBankList()
    {
        // verifies that we can create all records, and then recreate the text
        var banksFull = Data.Banks.GetBanks();
        var banksFullRecreated = Data.Banks.RecreateBankList(banksFull);
        Console.WriteLine(banksFullRecreated);
        Assert.Multiple(() =>
        {
            Assert.That(banksFullRecreated, Is.EqualTo(banksFull));
            Assert.That(Helpers.GetLines(Data.Banks.SourcePsv), Is.EqualTo(Helpers.GetLines(banksFull)), "Incorrect sortorder?");
        });
    }
}
