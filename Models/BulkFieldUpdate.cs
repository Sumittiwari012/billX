namespace MyWPFCRUDApp.Models
{
    // One (field, value) pair captured by BulkEditColumnsWindow and handed
    // back to ProductViewModel.ApplyBulkColumnUpdates. Value's real type
    // depends on FieldKey: MCategory for "CategoryId", MSubCategory for
    // "SubCategoryId", MUnit for "UnitId" — all picked from the existing
    // Category/SubCategory/Unit lists rather than typed in. Every other
    // key carries a plain string typed into a TextBox, parsed to the right
    // numeric type by ProductViewModel when it's applied.
    public class BulkFieldUpdate
    {
        public string FieldKey { get; set; } = string.Empty;
        public object? Value { get; set; }
    }
}