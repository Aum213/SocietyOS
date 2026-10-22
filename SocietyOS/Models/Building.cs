using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SocietyOS.Models
{
    public class Building
    {
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string Name { get; set; } = string.Empty; // e.g. "Tower B (Gulmohar)"

        [Required, MaxLength(10)]
        public string Code { get; set; } = string.Empty; // e.g. "B"

        public int TotalFloors { get; set; } = 10;

        public virtual ICollection<Flat> Flats { get; set; } = new List<Flat>();
    }
}