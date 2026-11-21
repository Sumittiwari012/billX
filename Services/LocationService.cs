using MyWPFCRUDApp.Models;
using System.Collections.Generic;

namespace MyWPFCRUDApp.Services
{
    /// <summary>Read-only lookup of godowns/stores for the stock screens. Master CRUD stays in your GodownService / StoreService.</summary>
    public static class LocationService
    {
        public static List<LocationOption> GetOptions(string type, bool activeOnly)
        {
            var m = StockService.Map(type);
            return Db.Query($"SELECT Id, {m.LocName} AS Name FROM {m.LocTable} {(activeOnly ? "WHERE IsActive = 1" : "")} ORDER BY {m.LocName}",
                r => new LocationOption { Id = Rd.Lng(r, "Id"), Name = Rd.Str(r, "Name"), Type = type });
        }
    }
}
