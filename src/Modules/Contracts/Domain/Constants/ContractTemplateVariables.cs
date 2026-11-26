namespace Sirh.Modules.Contracts.Domain.Constants;

public sealed record TemplateVariableDto(string Key, string Category, string Description);

public static class ContractTemplateVariables
{
    public static readonly IReadOnlyList<TemplateVariableDto> All =
    [
        new("employee.first_name", "Employee", "Prénom du collaborateur"),
        new("employee.last_name", "Employee", "Nom du collaborateur"),
        new("employee.full_name", "Employee", "Prénom et nom complets"),
        new("employee.honorific", "Employee", "Civilité (M, Mme, Autre)"),
        new("employee.email", "Employee", "Adresse e-mail professionnelle"),
        new("employee.born_at", "Employee", "Date de naissance (yyyy-MM-dd)"),
        new("employee.birth_place", "Employee", "Lieu de naissance"),
        new("employee.address", "Employee", "Adresse postale du collaborateur"),
        new("employee.postal_code", "Employee", "Code postal du collaborateur"),
        new("employee.city", "Employee", "Ville de résidence"),
        new("employee.national_id", "Employee", "Numéro de sécurité sociale"),

        new("company.name", "Company", "Raison sociale de l'entreprise"),
        new("company.legal_form", "Company", "Forme juridique (SAS, SARL...)"),
        new("company.address", "Company", "Adresse complète du siège"),
        new("company.siret", "Company", "Numéro SIRET (14 chiffres)"),
        new("company.register", "Company", "Immatriculation RCS"),
        new("company.capital", "Company", "Capital social"),
        new("company.owner", "Company", "Représentant légal"),
        new("company.collective_agreement", "Company", "Convention collective applicable"),

        new("position.job_title", "Position", "Intitulé du poste"),
        new("position.start_at", "Position", "Date de prise de poste"),
        new("position.end_at", "Position", "Date de fin (si applicable)"),
        new("position.hierarchical_position", "Position", "Statut hiérarchique (Cadre, ETAM...)"),
        new("position.coefficient", "Position", "Coefficient conventionnel"),
        new("position.rate", "Position", "Rémunération / taux"),
        new("position.time_contract_type", "Position", "Unité de temps (Hours / Days)"),
        new("position.time_contract_amount", "Position", "Volume horaire ou forfait jours"),
        new("position.location_name", "Position", "Nom du lieu d'affectation"),
        new("position.supervisor", "Position", "Maître d'apprentissage / tuteur")
    ];
}
