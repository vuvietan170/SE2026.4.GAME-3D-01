using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace CampusEscape.SaveSystem.Tests {
    [TestFixture]
    public class SaveSlotMutationTests {
        [Test]

        public void FailurePath_PersistenceFails_MustThrowExceptionAndNotReturnSuccess() {
            // Arrange
            var repo = new InMemorySaveSlotRepository();
            repo.SimulatePersistenceFailure = true; // Kích hoạt Mutation / DB Failure
            repo.Storage[201] = new SaveSlot {
                Id = 201, UserId = "userA", IsDeleted = false
            } ;

            var service = new SaveSlotService(repo);

            // Act & Assert (Nếu service trả success mà DB lưu lỗi -> Test này sẽ BẮT LỖI)
            var ex = Assert.ThrowsAsync < InvalidOperationException > (async() = > {await service.SoftDeleteAsync(201, "userA");
            });

            Assert.That(ex.Message, Contains.Substring("Persistence layer failed"));
        }
    }
}
