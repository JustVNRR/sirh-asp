namespace Sirh.Modules.HumanResources.Domain.Enums;

public enum EmployeeDocumentCategory
{
    PersonalFile = 0,
    PersonalEvents = 1,
    ContractualElements = 2,
    Disciplinary = 3,
    Departure = 4,
    Integration = 5
}

public enum EmployeeDocumentType
{
    // PersonalFile
    Cv = 0,
    VitalCard = 1,
    NationalIdCard = 2,
    SocialSecurityCertificate = 3,
    Passport = 4,
    ResidentPermit = 5,
    Iban = 6,
    RqthCertificate = 7,
    MutualAdhesionDia = 8,
    MutualAdhesionBia = 9,
    ExemptionForm = 10,
    SepaMandate = 11,
    ProvidentBulletin = 12,
    EmergencyContactSheet = 13,

    // PersonalEvents
    FamilyEventLeave = 100,
    MaternityLeave = 101,
    PaternityLeave = 102,
    ParentalLeave = 103,
    TrainingLeave = 104,
    BirthLeave = 105,
    OtherPersonalEvent = 106,

    // ContractualElements
    ContractElements = 200,
    Amendment = 201,
    Annex = 202,
    OtherContractualElement = 203,

    // Disciplinary
    FormalNotice = 300,
    Warning = 301,
    Blame = 302,
    PreliminaryInterviewInvitation = 303,
    DisciplinarySuspension = 304,
    Downgrading = 305,
    Dismissal = 306,
    OtherDisciplinary = 307,

    // Departure
    ResignationLetter = 400,
    DepartureInterviewNotice = 401,
    DepartureInterviewDocument = 402,
    MutualBreachAgreement = 403,
    InvitationRc = 404,
    CerfaRc = 405,
    NotificationRc = 406,
    DepartureNotification = 407,
    DismissalNotification = 408,
    OtherDeparture = 409,

    // Integration
    IntegrationFile = 500
}
