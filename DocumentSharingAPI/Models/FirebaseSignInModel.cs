using System.ComponentModel.DataAnnotations;
namespace DocumentSharingAPI.Models
{
    public class FirebaseSignInModel
    {
        [Required(ErrorMessage = "Firebase ID Token không được để trống")]
        public string IdToken { get; set; }

    }
}
