using System.ComponentModel.DataAnnotations;

namespace NalogZaUtovar;

public class NalogDto
{
    /// <summary>
    /// Broj skladista
    /// </summary>
    [Required]
    public string Skladiste { get; set; } = string.Empty;
    /// <summary>
    /// Naziv kupca
    /// </summary>
    [Required]
    public string Kupac { get; set; } = string.Empty;
    /// <summary>
    /// Registracija vozila
    /// </summary>
    [Required]
    public string Kamion { get; set; } = string.Empty;
    /// <summary>
    /// Broj naloga za utovar
    /// </summary>
    [Required]
    public int Nalog { get; set; }
}
