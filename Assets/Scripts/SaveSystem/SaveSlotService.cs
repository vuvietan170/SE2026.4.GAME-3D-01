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

        public SaveSlotService(ISaveSlotRepository repository) {
            _repository = repository;
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
            if (slot.UserId != currentUserId) {
                throw new UnauthorizedAccessException($"User {currentUserId} is not authorized to restore slot {slotId}.");
            }

            if (!slot.IsDeleted) {
                throw new InvalidOperationException($"Save slot {slotId} is not in deleted state (Conflict).");
            }

            slot.IsDeleted = false;
            slot.DeletedAt = null;
            slot.UpdatedAt = DateTime.UtcNow;

            bool saveResult = await _repository.SaveAsync(slot);
            if (!saveResult) {
                throw new InvalidOperationException("Persistence layer failed to restore state.");
            }

            return true;
        }
    }
}
