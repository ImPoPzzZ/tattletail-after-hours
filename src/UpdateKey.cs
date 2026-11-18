namespace KaleidoscopeEndless
{
    /// <summary>
    /// The PUBLIC half of the key that signs every release (the private half stays on the owner's PC, never in the repo).
    /// The mod and the updater only accept an update whose manifest is signed by it.
    /// </summary>
    public static class UpdateKey
    {
        public const string PublicXml =
            "<RSAKeyValue><Modulus>xDpypp2u7X7MfEJjUE08B56qvNTljBuqpO/ublh4kEcqHVVXCcqS59/hnhZJM6TIzIysSyR6WCf/GoHAAH3O789ivTuGfqbkRDIK1MvEtVqPoxTFj0fQWRF5AkAsr3ABzRyBHqFCfXOM9hnHUM2FkrIa+qinxRM7HghJs6R1oyNGjQbJAkL5R93DZqwsqrJxYghZ6Z4ctuFVJhKtomjZaP3A8GO2FbuHRdBog3trI6C4QaYrhnK7F2B7skcnBM0n7RPHAVBA96CBNXQD3g0P+7aCJ25GfaijrE3Q1UHnNaHTvWoEB639rimshHelMLavJj3K4VPtx+oUtls0uNdnBQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";
    }
}
