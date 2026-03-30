using System.ComponentModel.DataAnnotations;

namespace MeetingRoomBookingSystem.Models
{
    public class Room
    {
        public int RoomId { get; set; }

        [Required]
        [Display(Name = "Room Name")]
        public string RoomName { get; set; } = string.Empty;

        [Required]
        [Range(1, 100)]
        public int Capacity { get; set; }

        [Required]
        public string Location { get; set; } = string.Empty;
    }
}
