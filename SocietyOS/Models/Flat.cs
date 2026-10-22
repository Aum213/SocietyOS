using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SocietyOS.Models
{
    public class Flat
    {
        public int Id { get; set; }

        public int BuildingId { get; set; }
        public virtual Building Building { get; set; } = null!;

        [Required, MaxLength(10)]
        public string FlatNumber { get; set; } = string.Empty; // e.g. "204"

        public int Floor { get; set; } = 2;

        public virtual ICollection<ResidentProfile> Residents { get; set; } = new List<ResidentProfile>();
    }
}