using Sirh.Modules.HumanResources.Domain.Enums;

namespace Sirh.Modules.HumanResources.Domain.Constants;

public static class EmployeeDocumentTaxonomy
{
    public static EmployeeDocumentCategory GetCategory(EmployeeDocumentType documentType) => documentType switch
    {
        EmployeeDocumentType.Cv
            or EmployeeDocumentType.VitalCard
            or EmployeeDocumentType.NationalIdCard
            or EmployeeDocumentType.SocialSecurityCertificate
            or EmployeeDocumentType.Passport
            or EmployeeDocumentType.ResidentPermit
            or EmployeeDocumentType.Iban
            or EmployeeDocumentType.RqthCertificate
            or EmployeeDocumentType.MutualAdhesionDia
            or EmployeeDocumentType.MutualAdhesionBia
            or EmployeeDocumentType.ExemptionForm
            or EmployeeDocumentType.SepaMandate
            or EmployeeDocumentType.ProvidentBulletin
            or EmployeeDocumentType.EmergencyContactSheet => EmployeeDocumentCategory.PersonalFile,

        EmployeeDocumentType.FamilyEventLeave
            or EmployeeDocumentType.MaternityLeave
            or EmployeeDocumentType.PaternityLeave
            or EmployeeDocumentType.ParentalLeave
            or EmployeeDocumentType.TrainingLeave
            or EmployeeDocumentType.BirthLeave
            or EmployeeDocumentType.OtherPersonalEvent => EmployeeDocumentCategory.PersonalEvents,

        EmployeeDocumentType.ContractElements
            or EmployeeDocumentType.Amendment
            or EmployeeDocumentType.Annex
            or EmployeeDocumentType.OtherContractualElement => EmployeeDocumentCategory.ContractualElements,

        EmployeeDocumentType.FormalNotice
            or EmployeeDocumentType.Warning
            or EmployeeDocumentType.Blame
            or EmployeeDocumentType.PreliminaryInterviewInvitation
            or EmployeeDocumentType.DisciplinarySuspension
            or EmployeeDocumentType.Downgrading
            or EmployeeDocumentType.Dismissal
            or EmployeeDocumentType.OtherDisciplinary => EmployeeDocumentCategory.Disciplinary,

        EmployeeDocumentType.ResignationLetter
            or EmployeeDocumentType.DepartureInterviewNotice
            or EmployeeDocumentType.DepartureInterviewDocument
            or EmployeeDocumentType.MutualBreachAgreement
            or EmployeeDocumentType.InvitationRc
            or EmployeeDocumentType.CerfaRc
            or EmployeeDocumentType.NotificationRc
            or EmployeeDocumentType.DepartureNotification
            or EmployeeDocumentType.DismissalNotification
            or EmployeeDocumentType.OtherDeparture => EmployeeDocumentCategory.Departure,

        EmployeeDocumentType.IntegrationFile => EmployeeDocumentCategory.Integration,

        _ => throw new ArgumentOutOfRangeException(nameof(documentType), documentType, "Unsupported employee document type.")
    };

    public static bool IsSensitiveCategory(EmployeeDocumentCategory category) =>
        category is EmployeeDocumentCategory.Disciplinary or EmployeeDocumentCategory.Departure;

    public static bool IsSensitiveType(EmployeeDocumentType documentType) =>
        IsSensitiveCategory(GetCategory(documentType));

    public static bool IsSelfServiceWritableCategory(EmployeeDocumentCategory category) =>
        category is EmployeeDocumentCategory.PersonalFile or EmployeeDocumentCategory.PersonalEvents;
}
