using System.ComponentModel.DataAnnotations;

namespace NalogZaUtovar
{
    public class NalogUpdateDto
    {
            [Required]
            public required string Skladiste { get; set; }
            [Required]
            public required string Kupac { get; set; }
            [Required]
            public required string RegistracijaVozila { get; set; }
            public int? GodinaNaloga { get; set; }
            [Required]
            public int BrojNaloga { get; set; }
            [Required]
            public required string Dokument { get; set; } // Primalac Base64 stringa
            public byte[] DokumentBytes => Convert.FromBase64String(Dokument); // Pretvara u byte[]
            public string? QrSingl { get; set; }
            public string? QrFull { get; set; }
    }
}
