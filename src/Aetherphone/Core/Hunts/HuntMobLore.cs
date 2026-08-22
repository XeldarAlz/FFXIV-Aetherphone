using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Hunts;

internal static class HuntMobLore
{
    private static HuntMobTextCatalog? descriptions;
    private static HuntMobTextCatalog? tips;

    public static void Initialize(HuntMobTextCatalog descriptionCatalog, HuntMobTextCatalog tipCatalog)
    {
        descriptions = descriptionCatalog;
        tips = tipCatalog;
    }

    public static string? DescriptionFor(string mobId) => descriptions?.TextFor(mobId, HuntUiLanguage.Key());

    public static bool DescriptionIsFallback(string mobId) =>
        descriptions is not null && !descriptions.HasNativeText(mobId, Loc.Current.Code);

    public static string? TipFor(string mobId) => tips?.TextFor(mobId, HuntUiLanguage.Key());

    public static bool TipIsFallback(string mobId) =>
        tips is not null && !tips.HasNativeText(mobId, Loc.Current.Code);

    public static LocString? SpawnFor(string mobId) => mobId switch
    {
        "archaeotania" => L.HuntLore.ArchaeotaniaSpawn,
        "behemoth" => L.HuntLore.NoSpecificSpawnTrigger,
        "chi" => L.HuntLore.ChiSpawn,
        "coeurlregina" => L.HuntLore.NoSpecificSpawnTrigger,
        "daivadipa" => L.HuntLore.DaivadipaSpawn,
        "formidable" => L.HuntLore.FormidableSpawn,
        "ixion" => L.HuntLore.NoSpecificSpawnTrigger,
        "mica_the_magical_mu" => L.HuntLore.MicaTheMagicalMuSpawn,
        "noctilucale" => L.HuntLore.NoSpecificSpawnTrigger,
        "odin" => L.HuntLore.NoSpecificSpawnTrigger,
        "tamamo_gozen" => L.HuntLore.TamamoGozenSpawn,
        "ttokrrone" => L.HuntLore.TtokrroneSpawn,
        "aglaope" => L.HuntLore.AglaopeSpawn,
        "agrippa_the_mighty" => L.HuntLore.AgrippaTheMightySpawn,
        "arch_aethereater" => L.HuntLore.ArchAethereaterSpawn,
        "armstrong" => L.HuntLore.ArmstrongSpawn,
        "atticus_the_primogenitor" => L.HuntLore.AtticusThePrimogenitorSpawn,
        "bird_of_paradise" => L.HuntLore.BirdOfParadiseSpawn,
        "bone_crawler" => L.HuntLore.BoneCrawlerSpawn,
        "bonnacon" => L.HuntLore.BonnaconSpawn,
        "brontes" => L.HuntLore.BrontesSpawn,
        "burfurlur_the_canny" => L.HuntLore.BurfurlurTheCannySpawn,
        "chernobog" => L.HuntLore.ChernobogSpawn,
        "croakadile" => L.HuntLore.CroakadileSpawn,
        "croque_mitaine" => L.HuntLore.CroqueMitaineSpawn,
        "forgiven_pedantry" => L.HuntLore.ForgivenPedantrySpawn,
        "forgiven_rebellion" => L.HuntLore.ForgivenRebellionSpawn,
        "gamma" => L.HuntLore.GammaSpawn,
        "gandarewa" => L.HuntLore.GandarewaSpawn,
        "gunitt" => L.HuntLore.GunittSpawn,
        "ihnuxokiy" => L.HuntLore.IhnuxokiySpawn,
        "ixtab" => L.HuntLore.IxtabSpawn,
        "kaiser_behemoth" => L.HuntLore.KaiserBehemothSpawn,
        "ker" => L.HuntLore.KerSpawn,
        "kirlirger_the_abhorrent" => L.HuntLore.KirlirgerTheAbhorrentSpawn,
        "laideronnette" => L.HuntLore.LaideronnetteSpawn,
        "lampalagua" => L.HuntLore.BattlecraftOrGcLevequestSpawn,
        "leucrotta" => L.HuntLore.LeucrottaSpawn,
        "mindflayer" => L.HuntLore.MindflayerSpawn,
        "minhocao" => L.HuntLore.MinhocaoSpawn,
        "nandi" => L.HuntLore.NandiSpawn,
        "narrow_rift" => L.HuntLore.NarrowRiftSpawn,
        "neyoozoteel" => L.HuntLore.NeyoozoteelSpawn,
        "nunyunuwi" => L.HuntLore.NunyunuwiSpawn,
        "okina" => L.HuntLore.OkinaSpawn,
        "ophioneus" => L.HuntLore.OphioneusSpawn,
        "orghana" => L.HuntLore.OrghanaSpawn,
        "ruminator" => L.HuntLore.RuminatorSpawn,
        "safat" => L.HuntLore.SafatSpawn,
        "salt_and_light" => L.HuntLore.SaltAndLightSpawn,
        "sansheya" => L.HuntLore.SansheyaSpawn,
        "senmurv" => L.HuntLore.SenmurvSpawn,
        "sphatika" => L.HuntLore.SphatikaSpawn,
        "tarchia" => L.HuntLore.TarchiaSpawn,
        "the_forecaster" => L.HuntLore.TheForecasterSpawn,
        "the_garlok" => L.HuntLore.TheGarlokSpawn,
        "the_pale_rider" => L.HuntLore.ThePaleRiderSpawn,
        "thousand_cast_theda" => L.HuntLore.ThousandCastThedaSpawn,
        "tyger" => L.HuntLore.TygerSpawn,
        "udumbara" => L.HuntLore.UdumbaraSpawn,
        "wulgaru" => L.HuntLore.BattlecraftOrGcLevequestSpawn,
        "zona_seeker" => L.HuntLore.ZonaSeekerSpawn,
        _ => null,
    };
}
