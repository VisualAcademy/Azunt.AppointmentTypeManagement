using System.ComponentModel.DataAnnotations;

namespace Azunt.AppointmentTypeManagement
{
    public class AppointmentType
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string? AppointmentTypeName { get; set; }

        [Required]
        public bool IsActive { get; set; }

        [Required]
        public DateTime DateCreated { get; set; }
    }
}
