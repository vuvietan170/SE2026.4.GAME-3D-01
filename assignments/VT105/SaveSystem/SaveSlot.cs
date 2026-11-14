using System;

namespace CampusEscape.SaveSystem {
    public class SaveSlot {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string SaveName { get; set; }
        public string GameDataJson { get; set; }
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; } = null;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
