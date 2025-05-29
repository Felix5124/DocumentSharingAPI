namespace DocumentSharingAPI.Models
{
    public class DocumentTag
    {
        public int DocumentId { get; set; }
        public virtual Document Document { get; set; }

        public int TagId { get; set; }
        public virtual Tag Tag { get; set; }

    }
}
