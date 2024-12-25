using System.ComponentModel.DataAnnotations;

namespace NalogZaUtovar;

public class NalogPostDto
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
    public required string Dokument { get; set; }
    public byte[] DokumentBytes => Convert.FromBase64String(Dokument);
    public string? QrSingl { get; set; } = null;
    public byte[]? QrSinglBytes => QrSingl != null ? Convert.FromBase64String(QrSingl): null;
    public string? QrFull { get; set; } = null;
    public byte[]? QrFullBytes => QrFull != null ? Convert.FromBase64String(QrFull) : null;
}
