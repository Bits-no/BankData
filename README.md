# Clearingnumbers for Norway
[![NuGet Badge (BitsNo.Data)](https://buildstats.info/nuget/BitsNo.Data)](https://www.nuget.org/packages/BitsNo.Data)

In machine readable format from bits.no

## Data source
https://www.bits.no/iban/

# Example

```C#
int clearing = 0529;
var bank = Data.Banks.GetBankFromClearing(clearing);
Console.WriteLine(bank);                // 0529-0540|DNBANOKK|DNB Bank ASA
Console.WriteLine(bank?.ClearingStart); // 529
Console.WriteLine(bank?.ClearingEnd);   // 540
Console.WriteLine(bank?.BankName);      // DNB Bank ASA
Console.WriteLine(bank?.BIC);           // DNBANOKK
```
