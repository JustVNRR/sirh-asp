using Sirh.Modules.InternalLife.Domain.Enums;

namespace Sirh.Modules.InternalLife.Domain.Constants;

public static class CommonDocumentTaxonomy
{
    public static bool IsCseCategory(CommonDocumentCategory category) =>
        category is CommonDocumentCategory.CsePv
            or CommonDocumentCategory.CseBde
            or CommonDocumentCategory.HarassmentBestPractices;

    public static bool IsWelcomeKitCategory(CommonDocumentCategory category) =>
        category is CommonDocumentCategory.WelcomeKitMutual
            or CommonDocumentCategory.WelcomeKitForesight
            or CommonDocumentCategory.WelcomeKitEmergencyContact
            or CommonDocumentCategory.WelcomeKitLuncheonVoucher
            or CommonDocumentCategory.WelcomeKitBooklet
            or CommonDocumentCategory.WelcomeKitMobility;
}
