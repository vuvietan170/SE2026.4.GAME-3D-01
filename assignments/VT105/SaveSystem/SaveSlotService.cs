using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CampusEscape.SaveSystem {
    public interface ISaveSlotRepository {
        Task<SaveSlot> GetByIdAsync(int slotId);
        Task<bool> SaveAsync(SaveSlot slot);
        Task<IEnumerable<SaveSlot>> GetActiveByUserIdAsync(string userId);
    }

    public class SaveSlotService {
        private readonly ISaveSlotRepository _repository;
        private readonly SaveSlotPolicy _policy;
        private readonly Func<DateTime> _utcNow;

        public SaveSlotService(ISaveSlotRepository repository)
            : this(repository, new SaveSlotPolicy(), () => DateTime.UtcNow) { }

        public SaveSlotService(ISaveSlotRepository repository, SaveSlotPolicy policy, Func<DateTime> utcNow) {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
        }

        /// <summary>
        /// Thực hiện Xóa mềm (Soft Delete) save slot
        /// Risk coverage: Kiểm tra quyền sở hữu (userId == slot.UserId)
        /// </summary>
        public async Task<bool> SoftDeleteAsync(int slotId, string currentUserId) {
            var slot = await _repository.GetByIdAsync(slotId);
            if (slot == null) {
                throw new KeyNotFoundException($"Save slot {slotId} not found.");
            }

            // Guard Clause: Ngăn chặn phân quyền sai / rò rỉ dữ liệu chéo người dùng
            if (slot.UserId != currentUserId) {
                throw new UnauthorizedAccessException($"User {currentUserId} is not authorized to modify slot {slotId}.");
            }

            if (slot.IsDeleted) {
                return true; // Trạng thái đã xóa trước đó (Idempotent)
            }

            slot.IsDeleted = true;
            slot.DeletedAt = DateTime.UtcNow;
            
            bool saveResult = await _repository.SaveAsync(slot);
            if (!saveResult) {
                throw new InvalidOperationException("Persistence layer failed to save soft delete state.");
            }

            return true;
        }

        /// <summary>
        /// Thực hiện Khôi phục (Restore) save slot đã xóa mềm
        /// </summary>
        public async Task<bool> RestoreAsync(int slotId, string currentUserId) {
            var slot = await _repository.GetByIdAsync(slotId);
            if (slot == null) {
                throw new KeyNotFoundException($"Save slot {slotId} not found.");
            }

            // Guard Clause: Xác thực sở hữu
            if (string.IsNullOrWhiteSpace(currentUserId) || slot.UserId != currentUserId) {
                throw new UnauthorizedAccessException($"User {currentUserId} is not authorized to restore slot {slotId}.");
            }

            if (!slot.IsDeleted) {
                throw new InvalidOperationException($"Save slot {slotId} is not in deleted state (Conflict).");
            }

            var now = _utcNow();
            if (!slot.DeletedAt.HasValue || slot.DeletedAt.Value > now ||
                now - slot.DeletedAt.Value > TimeSpan.FromHours(_policy.RestoreRetentionHours)) {
                throw new InvalidOperationException($"Save slot {slotId} is outside the restore retention window.");
            }

            // Do not mutate the repository's read object before persistence succeeds.
            var restoredSlot = new SaveSlot {
                Id = slot.Id,
                UserId = slot.UserId,
                SaveName = slot.SaveName,
                GameDataJson = slot.GameDataJson,
                IsDeleted = false,
                DeletedAt = null,
                UpdatedAt = now
            };

            bool saveResult = await _repository.SaveAsync(restoredSlot);
            if (!saveResult) {
                throw new InvalidOperationException("Persistence layer failed to restore state.");
            }

            return true;
        }
    }
}
