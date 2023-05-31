namespace BitsNo.Data;

public class BankRecord
{
    private static void ValidateClearingRange(int clearing, string paramName)
    {
        if (9999 < clearing)
            throw new ArgumentOutOfRangeException(paramName, clearing, "Must be 4 numbers");
    }

    /// <summary>Create instance from pipe separated dataline</summary>
    public BankRecord(string[] psvData)
    {
        var s = psvData;
        if (s.Length != 3)
            throw new IndexOutOfRangeException("incorrect number of fields");

        var i = 0;
        var clearings = s[i++].Split('-');
        ClearingStart = int.Parse(clearings[0]);
        ClearingEnd = int.Parse(clearings[clearings.Length - 1]);
        ValidateClearingRange(ClearingStart, nameof(ClearingStart));
        ValidateClearingRange(ClearingEnd, nameof(ClearingEnd));
        BIC = s[i++];
        BankName = s[i++];
    }

    public string ClearingRange => ClearingStart == ClearingEnd ? $"{ClearingStart:0000}" : $"{ClearingStart:0000}-{ClearingEnd:0000}";

    public override string ToString() => $"{ClearingRange}|{BIC}|{BankName}";

    public bool MatchClearing(int clearingNumber) => ClearingStart <= clearingNumber && ClearingEnd >= clearingNumber;

    public int ClearingStart { get; }
    public int ClearingEnd { get; }
    public string BIC { get; }
    public string BankName { get; }
}
