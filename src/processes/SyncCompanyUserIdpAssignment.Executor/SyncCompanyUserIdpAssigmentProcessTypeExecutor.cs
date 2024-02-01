/********************************************************************************
 * Copyright (c) 2024 Contributors to the Eclipse Foundation
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

using Org.Eclipse.TractusX.Portal.Backend.Framework.ErrorHandling;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Repositories;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;
using Org.Eclipse.TractusX.Portal.Backend.Processes.Worker.Library;
using Org.Eclipse.TractusX.Portal.Backend.Provisioning.Library;
using System.Collections.Immutable;

namespace Org.Eclipse.TractusX.Portal.Backend.SyncCompanUserIdpAssignment.Executor;

public class SyncCompanyUserIdpAssigmentProcessTypeExecutor : IProcessTypeExecutor
{
    private readonly IPortalRepositories _portalRepositories;
    private readonly IProvisioningManager _provisioningManager;

    private static readonly IEnumerable<ProcessStepTypeId> _executableProcessSteps = ImmutableArray.Create(ProcessStepTypeId.SYNC_COMPANY_USER_IDP);

    public SyncCompanyUserIdpAssigmentProcessTypeExecutor(IPortalRepositories portalRepositories, IProvisioningManager provisioningManager)
    {
        _portalRepositories = portalRepositories;
        _provisioningManager = provisioningManager;
    }

    public ProcessTypeId GetProcessTypeId() => ProcessTypeId.SYNC_COMPANY_USER_IDP;
    public bool IsExecutableStepTypeId(ProcessStepTypeId processStepTypeId) => _executableProcessSteps.Contains(processStepTypeId);
    public IEnumerable<ProcessStepTypeId> GetExecutableStepTypeIds() => _executableProcessSteps;
    public ValueTask<bool> IsLockRequested(ProcessStepTypeId processStepTypeId) => new(false);

    public async ValueTask<IProcessTypeExecutor.InitializationResult> InitializeProcess(Guid processId, IEnumerable<ProcessStepTypeId> processStepTypeIds)
    {
        return await Task.FromResult(new IProcessTypeExecutor.InitializationResult(false, null));
    }

    public async ValueTask<IProcessTypeExecutor.StepExecutionResult> ExecuteProcessStep(ProcessStepTypeId processStepTypeId, IEnumerable<ProcessStepTypeId> processStepTypeIds, CancellationToken cancellationToken)
    {
        IEnumerable<ProcessStepTypeId>? nextStepTypeIds;
        ProcessStepStatusId stepStatusId;
        bool modified;
        string? processMessage;

        try
        {
            (nextStepTypeIds, stepStatusId, modified, processMessage) = processStepTypeId switch
            {
                ProcessStepTypeId.SYNC_COMPANY_USER_IDP => await SynchonizeNextCompanyUser().ConfigureAwait(false),
                _ => throw new UnexpectedConditionException($"unexpected processStepTypeId {processStepTypeId} for process {ProcessTypeId.SYNC_COMPANY_USER_IDP}")
            };
        }
        catch (Exception ex) when (ex is not SystemException)
        {
            (stepStatusId, processMessage, nextStepTypeIds) = ProcessError(ex);
            modified = true;
        }

        return new IProcessTypeExecutor.StepExecutionResult(modified, stepStatusId, nextStepTypeIds, null, processMessage);
    }

    private async Task<(IEnumerable<ProcessStepTypeId>? NextStepTypeIds, ProcessStepStatusId StepStatusId, bool Modified, string? ProcessMessage)> SynchonizeNextCompanyUser()
    {
        var userRepository = _portalRepositories.GetInstance<IUserRepository>();
        var companyUserIds = await userRepository.GetNextCompanyUserInfoForIdpLinDataSync().ToListAsync().ConfigureAwait(false);
        if (!companyUserIds.Any())
        {
            return (null, ProcessStepStatusId.DONE, false, "no companyUsers to add idp link data found");
        }

        var (companyUserId, userEntityId) = companyUserIds.First();
        userEntityId ??= await _provisioningManager.GetUserByUserName(companyUserId.ToString()).ConfigureAwait(false);
        var identityProviderRepository = _portalRepositories.GetInstance<IIdentityProviderRepository>();
        foreach (var idpInfo in await _provisioningManager.GetProviderUserLinkDataForCentralUserIdAsync(userEntityId ?? throw new ConflictException($"UserEntityId for company user {companyUserId} should always be set here.")).ToListAsync().ConfigureAwait(false))
        {
            var idpId = await identityProviderRepository.GetIdpIdByAlias(idpInfo.Alias).ConfigureAwait(false);
            if (idpId == Guid.Empty)
            {
                throw new ConflictException($"No Idp found for alias {idpInfo.Alias}");
            }

            userRepository.AddCompanyUserAssignedIdentityProvider(companyUserId, idpId, idpInfo.UserId, idpInfo.UserName);
        }

        var nextStepTypeIds = companyUserIds.Count > 1
            ? Enumerable.Repeat(ProcessStepTypeId.SYNC_COMPANY_USER_IDP, 1) // in case there are further serviceAccounts eligible for sync reschedule the same stepTypeId
            : null;
        return (nextStepTypeIds, ProcessStepStatusId.DONE, true, $"added idp link data to companyUser {companyUserId}");
    }

    private static (ProcessStepStatusId StatusId, string? ProcessMessage, IEnumerable<ProcessStepTypeId>? nextSteps) ProcessError(Exception ex) =>
        (ex is ServiceException { IsRecoverable: true } ? ProcessStepStatusId.TODO : ProcessStepStatusId.FAILED, ex.Message, null);
}
