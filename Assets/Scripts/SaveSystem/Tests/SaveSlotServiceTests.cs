using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace CampusEscape.SaveSystem.

Tests {
    // Fake Repository phục vụ Test Harness
    public class InMemorySaveSlotRepository :ISaveSlotRepository {
        public Dictionary<int, SaveSlot> Storage = new Dictionary<int, SaveSlot>();
        public bool SimulatePersistenceFailure = false;

        public Task<SaveSlot> GetByIdAsync ( int slotId)
        {
            Storage.TryGetValue(slotId, out var slot); return Task.FromResult(slot);
        }

        public Task<bool> SaveAsync (SaveSlot slot)
        {
            if (SimulatePersistenceFailure) {
                return Task.FromResult(false); // Giả lập persistence thất bại
            }
            Storage[slot.Id] = slot;
            return Task.FromResult(true);
        }

        public Task<IEnumerable<SaveSlot>> GetActiveByUserIdAsync (string userId)
        {
            var results = Storage.Values.Where(s = > s.UserId == userId && !s.IsDeleted);
            return Task.FromResult(results);
        }
    }

    [TestFixture]
    public class SaveSlotServiceTests {
        private InMemorySaveSlotRepository _repo;
        private SaveSlotService _service;

        [SetUp]

        public void Setup() {
            _repo = new InMemorySaveSlotRepository();
            _service = new SaveSlotService(_repo);
        }

        [Test]
        public async Task

        SoftDelete_ValidOwner_ChangesStateToDeleted() {
            // Arrange
            _repo.Storage[101] = new SaveSlot {
                Id = 101, UserId = "userA", IsDeleted = false
            } ;

            // Act
            bool result = await _service.SoftDeleteAsync(101, "userA");

            // Assert
            Assert.IsTrue(result);
            Assert.IsTrue(_repo.Storage[101].IsDeleted);
            Assert.IsNotNull(_repo.Storage[101].DeletedAt);
        }

        [Test]

        public void SoftDelete_OtherUserSlot_ThrowsUnauthorizedException() {
            // Arrange: Slot thuộc về userB, nhưng userA cố tình xóa
            _repo.Storage[102] = new SaveSlot {
                Id = 102, UserId = "userB", IsDeleted = false
            } ;

            // Act & Assert (Negative Test)
            var ex = Assert.ThrowsAsync < UnauthorizedAccessException > (async() = > {await _service.SoftDeleteAsync(102, "userA");
            });

            Assert.IsFalse(_repo.Storage[102].IsDeleted); // DB không bị đổi
        }

        [Test]
        public async Task

        Restore_ValidOwner_RestoresSlotState() {
            // Arrange
            _repo.Storage[103] = new SaveSlot {
                Id = 103, UserId = "userA", IsDeleted = true, DeletedAt = DateTime.UtcNow
            } ;

            // Act
            bool result = await _service.RestoreAsync(103, "userA");

            // Assert
            Assert.IsTrue(result);
            Assert.IsFalse(_repo.Storage[103].IsDeleted);
            Assert.IsNull(_repo.Storage[103].DeletedAt);
        }

        // ==========================================
        // PROPERTY-BASED TESTING (PBT) IMPLEMENTATION
        // ==========================================
        [Test]
        public async Task

        PropertyBasedTest_CrossTenantAccess_AlwaysDenied() {
            var random = new Random(42); // Random Seed
            int totalIterations = 100;

            for (int i = 0; i < totalIterations; i++) {
                string ownerUser = $ "user_{random.Next(1, 50)}";
                string attackerUser = $ "user_{random.Next(51, 100)}"; // Đảm bảo khác ownerUser
                int slotId = random.Next(1000, 9999);

                _repo.Storage[slotId] = new SaveSlot {
                    Id = slotId, UserId = ownerUser, IsDeleted = false
                } ;

                // Property 1: Mọi hành vi SoftDelete từ người dùng khác đều phải ném ngoại lệ Unauthorized
                Assert.ThrowsAsync<UnauthorizedAccessException>
                (async() = > {await _service.SoftDeleteAsync(slotId, attackerUser);
                });

                // Invariant Property 2: Trạng thái bản ghi gốc của Owner không bao giờ bị biến đổi
                Assert.IsFalse(_repo.Storage[slotId].IsDeleted, $"Invariant broken at iteration {i}");
            }
        }
    }
}
