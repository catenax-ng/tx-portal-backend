using Microsoft.EntityFrameworkCore;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Repositories;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Tests.Setup;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Entities;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;
using Xunit.Extensions.AssemblyFixture;

namespace Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Tests;

public class ProcessStepRepositoryTests : IAssemblyFixture<TestDbFixture>
{
    private readonly IFixture _fixture;
    private readonly TestDbFixture _dbTestDbFixture;

    public ProcessStepRepositoryTests(TestDbFixture testDbFixture)
    {
        _fixture = new Fixture().Customize(new AutoFakeItEasyCustomization { ConfigureMembers = true });
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => _fixture.Behaviors.Remove(b));

        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());
        _dbTestDbFixture = testDbFixture;
    }

    #region CreateProcessStep
    
    [Fact]
    public async Task CreateProcessStep_CreatesSuccessfully()
    {
        // Arrange
        var (sut, dbContext) = await CreateSutWithContext().ConfigureAwait(false);

        // Act
        sut.CreateProcessStep(ProcessStepTypeId.ACTIVATE_APPLICATION, ProcessStepStatusId.TODO);

        // Assert
        var changeTracker = dbContext.ChangeTracker;
        var changedEntries = changeTracker.Entries().ToList();
        changeTracker.HasChanges().Should().BeTrue();
        changedEntries.Should().NotBeEmpty();
        changedEntries.Should().HaveCount(1);
        var changedEntity = changedEntries.Single();
        changedEntity.State.Should().Be(EntityState.Added);
        changedEntity.Entity.Should().BeOfType<ProcessStep>().Which.ProcessStepStatusId.Should().Be(ProcessStepStatusId.TODO);
    }
    
    #endregion

    #region AttachAndModifyProcessStep
    
    [Fact]
    public async Task AttachAndModifyProcessStep_WithExistingProcessStep_UpdatesStatus()
    {
        // Arrange
        var (sut, dbContext) = await CreateSutWithContext().ConfigureAwait(false);

        // Act
        sut.AttachAndModifyProcessStep(new Guid("48f35f84-8d98-4fbd-ba80-8cbce5eeadb5"),
            existing =>
            {
                existing.ProcessStepStatusId = ProcessStepStatusId.TODO;
            },
            modify =>
            {
                modify.ProcessStepStatusId = ProcessStepStatusId.DONE;
            }
        );

        // Assert
        var changeTracker = dbContext.ChangeTracker;
        var changedEntries = changeTracker.Entries().ToList();
        changeTracker.HasChanges().Should().BeTrue();
        changedEntries.Should().NotBeEmpty();
        changedEntries.Should().HaveCount(1);
        var changedEntity = changedEntries.Single();
        changedEntity.State.Should().Be(EntityState.Modified);
        changedEntity.Entity.Should().BeOfType<ProcessStep>().Which.ProcessStepStatusId.Should().Be(ProcessStepStatusId.DONE);
    }
    
    #endregion

    #region GetProcessStepByApplicationIdInStatusTodo
    
    [Fact]
    public async Task GetProcessStepByApplicationIdInStatusTodo_WithTwoInTodo_ReturnsExpected()
    {
        // Arrange
        var sut = await CreateSut().ConfigureAwait(false);

        // Act
        var result = await sut
            .GetProcessStepByApplicationIdInStatusTodo(
                new Guid("2bb2005f-6e8d-41eb-967b-cde67546cafc"),
                new[] {ProcessStepTypeId.CREATE_BUSINESS_PARTNER_NUMBER_MANUAL, ProcessStepTypeId.VERIFY_REGISTRATION})
            .ToListAsync()
            .ConfigureAwait(false);

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(x => x.IsToDo.Should().BeTrue());
    }
    
    [Fact]
    public async Task GetProcessStepByApplicationIdInStatusTodo_WithAllStepsInDone_ReturnsEmpty()
    {
        // Arrange
        var sut = await CreateSut().ConfigureAwait(false);

        // Act
        var result = await sut
            .GetProcessStepByApplicationIdInStatusTodo(
                new Guid("4829b64c-de6a-426c-81fc-c0bcf95bcb76"),
                new[] {ProcessStepTypeId.CREATE_BUSINESS_PARTNER_NUMBER_MANUAL, ProcessStepTypeId.VERIFY_REGISTRATION})
            .ToListAsync()
            .ConfigureAwait(false);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    private async Task<(ProcessStepRepository sut, PortalDbContext dbContext)> CreateSutWithContext()
    {
        var context = await _dbTestDbFixture.GetPortalDbContext().ConfigureAwait(false);
        var sut = new ProcessStepRepository(context);
        return (sut, context);
    }
    
    private async Task<ProcessStepRepository> CreateSut()
    {
        var context = await _dbTestDbFixture.GetPortalDbContext().ConfigureAwait(false);
        var sut = new ProcessStepRepository(context);
        return sut;
    }
}