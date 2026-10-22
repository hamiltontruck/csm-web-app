using System.ComponentModel.DataAnnotations;

namespace CSM.Web.Models.Companies;

public sealed class CompanyModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string BaseCurrencyCode { get; set; } = "";
    public bool IsActive { get; set; }
}

public sealed class CreateCompanyModel
{
    [Required(ErrorMessage = "Company code is required."), StringLength(30)]
    public string Code { get; set; } = "";
    [Required(ErrorMessage = "Company name is required."), StringLength(200)]
    public string Name { get; set; } = "";
    [Required, RegularExpression("^[A-Za-z]{3}$", ErrorMessage = "Enter a three-letter currency code.")]
    public string BaseCurrencyCode { get; set; } = "ETB";
    [EmailAddress, StringLength(200)]
    public string? Email { get; set; }
    [StringLength(50)]
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
}
