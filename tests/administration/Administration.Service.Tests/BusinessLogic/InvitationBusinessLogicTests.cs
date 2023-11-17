/********************************************************************************
 * Copyright (c) 2021, 2023 BMW Group AG
 * Copyright (c) 2021, 2023 Contributors to the Eclipse Foundation
 *
 * See the NOTICE file(s) distributed with this work for additional
 * information regarding copyright ownership.
 *
 * This program and the accompanying materials are made available under the
 * terms of the Apache License, Version 2.0 which is available at
 * https://www.apache.org/licenses/LICENSE-2.0.
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations
 * under the License.
 *
 * SPDX-License-Identifier: Apache-2.0
 ********************************************************************************/

using Org.Eclipse.TractusX.Portal.Backend.Administration.Service.BusinessLogic;
using Org.Eclipse.TractusX.Portal.Backend.Administration.Service.Models;
using Org.Eclipse.TractusX.Portal.Backend.Framework.ErrorHandling;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Repositories;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Entities;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;
using Org.Eclipse.TractusX.Portal.Backend.Tests.Shared.Extensions;

namespace Org.Eclipse.TractusX.Portal.Backend.Administration.Service.Tests.BusinessLogic;

public class InvitationBusinessLogicTests
{
    private readonly IFixture _fixture;
    private readonly IProcessStepRepository _processStepRepository;
    private readonly ICompanyInvitationRepository _companyInvitationRepository;
    private readonly InvitationBusinessLogic _sut;

    public InvitationBusinessLogicTests()
    {
        _fixture = new Fixture().Customize(new AutoFakeItEasyCustomization { ConfigureMembers = true });
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        var portalRepositories = A.Fake<IPortalRepositories>();
        _processStepRepository = A.Fake<IProcessStepRepository>();
        _companyInvitationRepository = A.Fake<ICompanyInvitationRepository>();

        A.CallTo(() => portalRepositories.GetInstance<IProcessStepRepository>()).Returns(_processStepRepository);
        A.CallTo(() => portalRepositories.GetInstance<ICompanyInvitationRepository>()).Returns(_companyInvitationRepository);

        _sut = new InvitationBusinessLogic(portalRepositories);
    }

    #region ExecuteInvitation

    [Fact]
    public async Task ExecuteInvitation_WithoutEmail_ThrowsControllerArgumentException()
    {
        var invitationData = _fixture.Build<CompanyInvitationData>()
            .With(x => x.OrganisationName, _fixture.Create<string>())
            .WithNamePattern(x => x.FirstName)
            .WithNamePattern(x => x.LastName)
            .With(x => x.Email, (string?)null)
            .Create();

        async Task Act() => await _sut.ExecuteInvitation(invitationData).ConfigureAwait(false);

        var ex = await Assert.ThrowsAsync<ControllerArgumentException>(Act);
        ex.Message.Should().Be("email must not be empty (Parameter 'email')");
        ex.ParamName.Should().Be("email");
    }

    [Fact]
    public async Task ExecuteInvitation_WithoutOrganisationName_ThrowsControllerArgumentException()
    {
        var invitationData = _fixture.Build<CompanyInvitationData>()
            .With(x => x.OrganisationName, (string?)null)
            .WithNamePattern(x => x.FirstName)
            .WithNamePattern(x => x.LastName)
            .WithEmailPattern(x => x.Email)
            .Create();

        async Task Act() => await _sut.ExecuteInvitation(invitationData).ConfigureAwait(false);

        var ex = await Assert.ThrowsAsync<ControllerArgumentException>(Act);
        ex.Message.Should().Be("organisationName must not be empty (Parameter 'organisationName')");
        ex.ParamName.Should().Be("organisationName");
    }

    [Fact]
    public async Task ExecuteInvitation_WithValidData_CreatesExpected()
    {
        var processes = new List<Process>();
        var processSteps = new List<ProcessStep>();
        var invitations = new List<CompanyInvitation>();
        SetupFakes(processes, processSteps, invitations);

        var invitationData = _fixture.Build<CompanyInvitationData>()
            .With(x => x.OrganisationName, _fixture.Create<string>())
            .With(x => x.UserName, "testUserName")
            .WithNamePattern(x => x.FirstName)
            .WithNamePattern(x => x.LastName)
            .WithEmailPattern(x => x.Email)
            .Create();

        await _sut.ExecuteInvitation(invitationData).ConfigureAwait(false);

        processes.Should().ContainSingle().And.Satisfy(x => x.ProcessTypeId == ProcessTypeId.INVITATION);
        processSteps.Should().ContainSingle().And.Satisfy(x => x.ProcessStepTypeId == ProcessStepTypeId.INVITATION_SETUP_IDP && x.ProcessStepStatusId == ProcessStepStatusId.TODO);
        invitations.Should().ContainSingle().And.Satisfy(x => x.ProcessId == processes.Single().Id && x.UserName == "testUserName");
    }

    #endregion

    #region Setup

    private void SetupFakes(List<Process> processes, List<ProcessStep> processSteps, List<CompanyInvitation> invitations)
    {
        var createdProcessId = Guid.NewGuid();
        A.CallTo(() => _processStepRepository.CreateProcess(ProcessTypeId.INVITATION))
            .Invokes((ProcessTypeId processTypeId) =>
            {
                var process = new Process(createdProcessId, processTypeId, Guid.NewGuid());
                processes.Add(process);
            })
            .Returns(new Process(createdProcessId, ProcessTypeId.INVITATION, Guid.NewGuid()));
        A.CallTo(() => _processStepRepository.CreateProcessStep(ProcessStepTypeId.INVITATION_SETUP_IDP, ProcessStepStatusId.TODO, createdProcessId))
            .Invokes((ProcessStepTypeId processStepTypeId, ProcessStepStatusId processStepStatusId, Guid processId) =>
            {
                var processStep = new ProcessStep(Guid.NewGuid(), processStepTypeId, processStepStatusId, processId,
                    DateTimeOffset.UtcNow);
                processSteps.Add(processStep);
            });

        A.CallTo(() => _companyInvitationRepository.CreateCompanyInvitation(A<string>._, A<string>._, A<string>._, A<string>._, createdProcessId, A<Action<CompanyInvitation>>._))
            .Invokes((string firstName, string lastName, string email, string organisationName, Guid processId, Action<CompanyInvitation>? setOptionalFields) =>
            {
                var entity = new CompanyInvitation(Guid.NewGuid(), firstName, lastName, email, organisationName, processId);
                setOptionalFields?.Invoke(entity);
                invitations.Add(entity);
            });
    }

    #endregion

    [Serializable]
    public class TestException : Exception
    {
        public TestException() { }
        public TestException(string message) : base(message) { }
        public TestException(string message, Exception inner) : base(message, inner) { }
        protected TestException(
            System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
