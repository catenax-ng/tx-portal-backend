using Org.Eclipse.TractusX.Portal.Backend.Clearinghouse.Library.BusinessLogic;
using Org.Eclipse.TractusX.Portal.Backend.Clearinghouse.Library.Models;
using Org.Eclipse.TractusX.Portal.Backend.Framework.ErrorHandling;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Repositories;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Entities;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;

namespace Org.Eclipse.TractusX.Portal.Backend.Clearinghouse.Library.Tests;

public class ClearinghouseBusinessLogicTests
{
    private static readonly Guid IdWithBpn = new ("c244f79a-7faf-4c59-bb85-fbfdf72ce46f");
    private const string ValidBpn = "BPNL123698762345";
    private const string FailingBpn = "FAILINGBPN";

    private readonly IFixture _fixture;
    
    private readonly IApplicationRepository _applicationRepository;
    private readonly IApplicationChecklistRepository _applicationChecklistRepository;
    private readonly IPortalRepositories _portalRepositories;
    
    private readonly ClearinghouseBusinessLogic _logic;
    private readonly IClearinghouseService _clearinghouseService;

    public ClearinghouseBusinessLogicTests()
    {
        _fixture = new Fixture().Customize(new AutoFakeItEasyCustomization {ConfigureMembers = true});
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        _applicationRepository = A.Fake<IApplicationRepository>();
        _applicationChecklistRepository = A.Fake<IApplicationChecklistRepository>();
        _portalRepositories = A.Fake<IPortalRepositories>();
        _clearinghouseService = A.Fake<IClearinghouseService>();

        A.CallTo(() => _portalRepositories.GetInstance<IApplicationRepository>()).Returns(_applicationRepository);
        A.CallTo(() => _portalRepositories.GetInstance<IApplicationChecklistRepository>()).Returns(_applicationChecklistRepository);

        _logic = new ClearinghouseBusinessLogic(_portalRepositories, _clearinghouseService);
    }
    
    #region ProcessClearinghouseResponse

    [Fact]
    public async Task ProcessClearinghouseResponseAsync_WithClearinghouseInTodo_ThrowsConflictException()
    {
        // Arrange
        var data = _fixture.Build<ClearinghouseResponseData>()
            .Create();
        SetupForProcessClearinghouseResponse();

        // Act
        async Task Act() => await _logic.ProcessClearinghouseResponseAsync(FailingBpn, data, CancellationToken.None).ConfigureAwait(false);

        // Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(Act);
        ex.Message.Should().Be($"Checklist Item {ApplicationChecklistEntryTypeId.CLEARING_HOUSE} is not in status {ApplicationChecklistEntryStatusId.IN_PROGRESS}");
    }
    
    [Fact]
    public async Task ProcessClearinghouseResponseAsync_WithConfirmation_UpdatesEntry()
    {
        
        // Arrange
        var entry = new ApplicationChecklistEntry(IdWithBpn, ApplicationChecklistEntryTypeId.IDENTITY_WALLET, ApplicationChecklistEntryStatusId.TO_DO, DateTimeOffset.UtcNow);
        var data = _fixture.Build<ClearinghouseResponseData>()
            .With(x => x.Status, ClearinghouseResponseStatus.CONFIRM)
            .With(x => x.Message, (string?)null)
            .Create();
        SetupForProcessClearinghouseResponse(entry);

        // Act
        await _logic.ProcessClearinghouseResponseAsync(ValidBpn, data, CancellationToken.None).ConfigureAwait(false);

        // Assert
        A.CallTo(() => _applicationChecklistRepository.AttachAndModifyApplicationChecklist(IdWithBpn, ApplicationChecklistEntryTypeId.CLEARING_HOUSE, A<Action<ApplicationChecklistEntry>>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _portalRepositories.SaveAsync()).MustHaveHappenedOnceExactly();
        entry.Comment.Should().BeNull();
        entry.ApplicationChecklistEntryStatusId.Should().Be(ApplicationChecklistEntryStatusId.DONE);
    }

    [Fact]
    public async Task ProcessClearinghouseResponseAsync_WithDecline_UpdatesEntry()
    {
        // Arrange
        var entry = new ApplicationChecklistEntry(IdWithBpn, ApplicationChecklistEntryTypeId.IDENTITY_WALLET, ApplicationChecklistEntryStatusId.TO_DO, DateTimeOffset.UtcNow);
        var data = _fixture.Build<ClearinghouseResponseData>()
            .With(x => x.Status, ClearinghouseResponseStatus.DECLINE)
            .With(x => x.Message, "Comment about the error")
            .Create();
        SetupForProcessClearinghouseResponse(entry);

        // Act
        await _logic.ProcessClearinghouseResponseAsync(ValidBpn, data, CancellationToken.None).ConfigureAwait(false);

        // Assert
        A.CallTo(() => _applicationChecklistRepository.AttachAndModifyApplicationChecklist(IdWithBpn, ApplicationChecklistEntryTypeId.CLEARING_HOUSE, A<Action<ApplicationChecklistEntry>>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _portalRepositories.SaveAsync()).MustHaveHappenedOnceExactly();
        entry.Comment.Should().Be("Comment about the error");
        entry.ApplicationChecklistEntryStatusId.Should().Be(ApplicationChecklistEntryStatusId.FAILED);
    }

    #endregion
    
    #region Setup
    
    private void SetupForProcessClearinghouseResponse(ApplicationChecklistEntry? applicationChecklistEntry = null)
    {
        if (applicationChecklistEntry != null)
        {
            SetupForUpdate(applicationChecklistEntry);
        }

        A.CallTo(() => _applicationRepository.GetSubmittedIdAndClearinghouseChecklistStatusByBpn(ValidBpn))
            .ReturnsLazily(() => new ValueTuple<Guid, ApplicationChecklistEntryStatusId>(IdWithBpn, ApplicationChecklistEntryStatusId.IN_PROGRESS));
        A.CallTo(() => _applicationRepository.GetSubmittedIdAndClearinghouseChecklistStatusByBpn(FailingBpn))
            .ReturnsLazily(() => new ValueTuple<Guid, ApplicationChecklistEntryStatusId>(IdWithBpn, ApplicationChecklistEntryStatusId.TO_DO));
        A.CallTo(() => _applicationRepository.GetSubmittedIdAndClearinghouseChecklistStatusByBpn(A<string>.That.Not.Matches(x => x == ValidBpn || x == FailingBpn)))
            .ReturnsLazily(() => new ValueTuple<Guid, ApplicationChecklistEntryStatusId>());
    }

    private void SetupForUpdate(ApplicationChecklistEntry applicationChecklistEntry)
    {
        A.CallTo(() => _applicationChecklistRepository.AttachAndModifyApplicationChecklist(A<Guid>._, A<ApplicationChecklistEntryTypeId>._, A<Action<ApplicationChecklistEntry>>._))
            .Invokes((Guid _, ApplicationChecklistEntryTypeId _, Action<ApplicationChecklistEntry> setFields) =>
            {
                applicationChecklistEntry.DateLastChanged = DateTimeOffset.UtcNow;
                setFields.Invoke(applicationChecklistEntry);
            });

        A.CallTo(() => _portalRepositories.GetInstance<IApplicationChecklistRepository>()).Returns(_applicationChecklistRepository);
    }

    #endregion
}