namespace Epipharm.Data
{
    /// <summary>One row of a service picker (F5 lookup on "Code Service").
    /// Scoped to the one establishment (CODE_ET='23124010') confirmed
    /// against a real screenshot of the F5 "Liste des Services
    /// Utilisateurs" popup — see ServiceRepository.</summary>
    public class ServiceLookup
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }

        public string Label => $"{Code}  {Name}";
    }
}
