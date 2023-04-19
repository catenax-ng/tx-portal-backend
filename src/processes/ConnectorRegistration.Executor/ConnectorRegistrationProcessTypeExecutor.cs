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

using Microsoft.Extensions.Options;
using Org.Eclipse.TractusX.Portal.Backend.Administration.Service.BusinessLogic;
using Org.Eclipse.TractusX.Portal.Backend.Daps.Library;
using Org.Eclipse.TractusX.Portal.Backend.Framework.ErrorHandling;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Models;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Repositories;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;
using Org.Eclipse.TractusX.Portal.Backend.Processes.Worker.Library;
using Org.Eclipse.TractusX.Portal.Backend.SdFactory.Library.BusinessLogic;
using System.Collections.Immutable;

namespace Org.Eclipse.TractusX.Portal.Backend.Processes.ConnectorRegistration.Executor;

public class ConnectorRegistrationProcessTypeExecutor : IProcessTypeExecutor
{
    private readonly IPortalRepositories _portalRepositories;
    private readonly IDapsService _dapsService;
    private readonly ISdFactoryBusinessLogic _sdFactoryBusinessLogic;
    private readonly ConnectorsSettings _settings;
    private readonly ImmutableDictionary<ProcessStepTypeId,(bool IsLockRequested, Func<Guid,CancellationToken,Task> ProcessFunc)> _stepExecutions;

    private Guid connectorId;
    private string? businessPartnerNumber;
    private ConnectorDapsProcessData? dapsProcessData;
    private Guid? selfDescriptionDocumentId;

    public ConnectorRegistrationProcessTypeExecutor(IPortalRepositories portalRepositories, IOptions<ConnectorsSettings> options, IDapsService dapsService, ISdFactoryBusinessLogic sdFactoryBusinessLogic)
    {
        _portalRepositories = portalRepositories;
        _dapsService = dapsService;
        _sdFactoryBusinessLogic = sdFactoryBusinessLogic;
        _settings = options.Value;

        var foo = new (ProcessStepTypeId ProcessStepTypeId, (bool IsLockRequested, Func<Guid,CancellationToken,Task> ProcessFunc) Execution)[] {
            (ProcessStepTypeId.CALL_DAPS_AUTHENTICATION, (true, Foo)),
            (ProcessStepTypeId.START_REGISTER_CONNECTOR_SD, (true, Foo))
        };
    }

    public Task Foo(Guid processId, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public ProcessTypeId GetProcessTypeId() => ProcessTypeId.CONNECTOR_REGISTRATION;
    public bool IsExecutableStepTypeId(ProcessStepTypeId processStepTypeId) => processStepTypeId switch {
        ProcessStepTypeId.CALL_DAPS_AUTHENTICATION => true,
        ProcessStepTypeId.START_REGISTER_CONNECTOR_SD => true,
        _ => throw new ConflictException($"processStepTypeId {processStepTypeId} should never be associated with process-type CONNECTOR_REGISTRATION")
    };
    public IEnumerable<ProcessStepTypeId> GetExecutableStepTypeIds() => new [] { ProcessStepTypeId.CALL_DAPS_AUTHENTICATION, ProcessStepTypeId.START_REGISTER_CONNECTOR_SD };
    public ValueTask<bool> IsLockRequested(ProcessStepTypeId processStepTypeId) => ValueTask.FromResult(IsExecutableStepTypeId(processStepTypeId));

    public async ValueTask<IProcessTypeExecutor.InitializationResult> InitializeProcess(Guid processId, IEnumerable<ProcessStepTypeId> processStepTypeIds)
    {
        try
        {
            var result = await _portalRepositories.GetInstance<IConnectorsRepository>().GetConnectorProcessData(processId, processStepTypeIds.Contains(ProcessStepTypeId.CALL_DAPS_AUTHENTICATION));
            if (!result.IsValidProcessId)
            {
                throw new NotFoundException($"processId {processId} does not exist");
            }
            if (result.ConnectorProcessData.ConnectorId == Guid.Empty)
            {
                throw new ConflictException($"processId {processId} is not associated with any connector");
            }
            if (string.IsNullOrEmpty(result.ConnectorProcessData.BusinessPartnerNumber))
            {
                throw new ConflictException($"businessPartnerNumber associated with connector {connectorId} must not be null or empty");
            }
            (connectorId, businessPartnerNumber, dapsProcessData, selfDescriptionDocumentId) = result.ConnectorProcessData;
            return new IProcessTypeExecutor.InitializationResult(false, null);
        }
        catch (Exception)
        {
            (connectorId, businessPartnerNumber, dapsProcessData, selfDescriptionDocumentId) = (Guid.Empty, null, null, null);
            throw;
        }
    }

    public ValueTask<IProcessTypeExecutor.StepExecutionResult> ExecuteProcessStep(ProcessStepTypeId processStepTypeId, IEnumerable<ProcessStepTypeId> processStepTypeIds, CancellationToken cancellationToken)
    {
        if (connectorId == Guid.Empty)
        {
            throw new UnexpectedConditionException("connectorId should never be null or empty here");
        }
        return processStepTypeId switch
        {
            ProcessStepTypeId.CALL_DAPS_AUTHENTICATION => ExecuteCallDapsAuthentication(processStepTypeIds, cancellationToken),
            ProcessStepTypeId.START_REGISTER_CONNECTOR_SD => ExecuteRegisterConnector(processStepTypeIds, cancellationToken),
            _ => throw new UnexpectedConditionException($"processStepType should always be an exectable processStep here (ProcessStepType: {processStepTypeId})")
        };
    }

    public async ValueTask<IProcessTypeExecutor.StepExecutionResult> ExecuteCallDapsAuthentication(IEnumerable<ProcessStepTypeId> processStepTypeIds, CancellationToken cancellationToken)
    {
        if (dapsProcessData == null)
        {
            throw new UnexpectedConditionException("dapsProcessData should never be null here");
        }
        if (string.IsNullOrEmpty(businessPartnerNumber))
        {
            throw new ConflictException($"businessPartnerNumber associated with connector {connectorId} must not be null or empty");
        }
        if (dapsProcessData.CertificateName == null || dapsProcessData.CertificateContent == null)
        {
            throw new ConflictException($"connector {connectorId} is not associated with any certificate-document");
        }
        await _dapsService
            .EnableDapsAuthAsync(
                dapsProcessData.ConnectorName,
                dapsProcessData.ConnectorUrl,
                businessPartnerNumber,
                dapsProcessData.CertificateName,
                dapsProcessData.CertificateContent,
                dapsProcessData.CertificateMediaTypeId,
                cancellationToken)
            .ConfigureAwait(false);
        _portalRepositories
            .GetInstance<IConnectorsRepository>().AttachAndModifyConnector(connectorId, con =>
        {
            con.DapsRegistrationSuccessful = true;
            con.StatusId = ConnectorStatusId.ACTIVE;
        });
        return new IProcessTypeExecutor.StepExecutionResult(true, ProcessStepStatusId.DONE, null, null, null);
    }

    public async ValueTask<IProcessTypeExecutor.StepExecutionResult> ExecuteRegisterConnector(IEnumerable<ProcessStepTypeId> processStepTypeIds, CancellationToken cancellationToken)
    {
        if (selfDescriptionDocumentId == null)
        {
            throw new ConflictException($"connector {connectorId} is not associated with a selfDescriptionDocument");
        }
        if (string.IsNullOrEmpty(businessPartnerNumber))
        {
            throw new ConflictException($"businessPartnerNumber associated with connector {connectorId} must not be null or empty");
        }
        var selfDescriptionDocumentUrl = $"{_settings.SelfDescriptionDocumentUrl}/{selfDescriptionDocumentId}";
        await _sdFactoryBusinessLogic
            .RegisterConnectorAsync(connectorId, selfDescriptionDocumentUrl, businessPartnerNumber, cancellationToken)
            .ConfigureAwait(false);

        return new IProcessTypeExecutor.StepExecutionResult(true, ProcessStepStatusId.DONE, null, null, null);
    }
}
