namespace BitsNo.Tests;

public class SimpleValidationsTests
{
    [Test]
    public void SourcePsvHasDataTest() =>
        Assert.That(Data.Banks.SourcePsv, Is.Not.Null.Or.Empty,
            Data.Banks.SourcePsv);

    [TestCase("source.psv")]
    public void SourceFileHasCorrectEncoding(string fileName)
    {
        var fileData = Helpers.FindAndReadTextFile(fileName);
        Assert.That(fileData, Helpers.ContainsAnyUmlaut(),
            fileData);
    }

    [Test]
    public void SourcePsvHasCorrectEncoding() =>
        Assert.That(Data.Banks.SourcePsv, Helpers.ContainsAnyUmlaut());

    [TestCase("33000-3300||", "ClearingStart", 33000)]
    [TestCase("3300-33000||", "ClearingEnd", 33000)]
    public void BankRecordCtorClearingThrowsTest(string line, string paramName, int actual)
    {
        var ex = Assert.Catch<ArgumentOutOfRangeException>(() => Data.Banks.GetList(line));
        Console.WriteLine(ex.ToString());
        Assert.Multiple(() =>
        {
            Assert.That(ex, Has.Message.StartsWith($"Must be 4 numbers (Parameter"));
            Assert.That(ex, Has.Message.Contains($"(Parameter '{paramName}')"));
            Assert.That(ex, Has.Message.Contains($"Actual value was {actual}."));
        });
    }

    [TestCase("3x-||")]
    [TestCase("300-3x||")]
    public void BankRecordCtorThrowsFormatExceptionTest(string line)
    {
        var ex = Assert.Catch<FormatException>(() => Data.Banks.GetList(line));
        Console.WriteLine(ex.ToString());
        Assert.That(ex, Has.Message.Contains(" was not in a correct format."));
    }

    [TestCase("9710-9719|")]
    [TestCase("9710-9719|x|x Bank|x")]
    public void BankRecordMissingFieldsCtorThrowsIndexOutOfRangeExceptionExceptionTest(string line)
    {
        var ex = Assert.Catch<IndexOutOfRangeException>(() => Data.Banks.GetList(line));
        Console.WriteLine(ex.ToString());
    }
}
