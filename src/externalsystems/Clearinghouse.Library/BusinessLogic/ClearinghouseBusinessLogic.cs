﻿/********************************************************************************
 * Copyright (c) 2021, 2023 Microsoft and BMW Group AG
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

using Org.Eclipse.TractusX.Portal.Backend.Checklist.Library;
using Org.Eclipse.TractusX.Portal.Backend.Clearinghouse.Library.Models;
using Org.Eclipse.TractusX.Portal.Backend.Custodian.Library.BusinessLogic;
using Org.Eclipse.TractusX.Portal.Backend.Framework.ErrorHandling;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Repositories;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Entities;
using System.Collections.Immutable;

namespace Org.Eclipse.TractusX.Portal.Backend.Clearinghouse.Library.BusinessLogic;

public class ClearinghouseBusinessLogic : IClearinghouseBusinessLogic
{
    private readonly IPortalRepositories _portalRepositories;
    private readonly IClearinghouseService _clearinghouseService;
    private readonly ICustodianBusinessLogic _custodianBusinessLogic;
    private readonly IChecklistService _checklistService;

    public ClearinghouseBusinessLogic(IPortalRepositories portalRepositories, IClearinghouseService clearinghouseService, ICustodianBusinessLogic custodianBusinessLogic, IChecklistService checklistService)
    {
        _portalRepositories = portalRepositories;
        _clearinghouseService = clearinghouseService;
        _custodianBusinessLogic = custodianBusinessLogic;
        _checklistService = checklistService;
    }

    public async Task<(Action<ApplicationChecklistEntry>?,IEnumerable<ProcessStep>?,bool)> HandleStartClearingHouse(IChecklistService.WorkerChecklistProcessStepData context, CancellationToken cancellationToken)
    {
        var walletData = await _custodianBusinessLogic.GetWalletByBpnAsync(context.ApplicationId, cancellationToken);
        if (walletData == null || string.IsNullOrEmpty(walletData.Did))
        {
            throw new ConflictException($"Decentralized Identifier for application {context.ApplicationId} is not set");
        }

        await TriggerCompanyDataPost(context.ApplicationId, walletData.Did, cancellationToken).ConfigureAwait(false);

        _checklistService.ScheduleProcessSteps(context, new [] { ProcessStepTypeId.END_CLEARING_HOUSE });
        return (entry => entry.ApplicationChecklistEntryStatusId = ApplicationChecklistEntryStatusId.IN_PROGRESS, null, true);
    }

    private async Task TriggerCompanyDataPost(Guid applicationId, string decentralizedIdentifier, CancellationToken cancellationToken)
    {
        var data = await _portalRepositories.GetInstance<IApplicationRepository>()
            .GetClearinghouseDataForApplicationId(applicationId).ConfigureAwait(false);
        if (data is null)
        {
            throw new ConflictException($"Application {applicationId} does not exists.");
        }

        if (data.ApplicationStatusId != CompanyApplicationStatusId.SUBMITTED)
        {
            throw new ConflictException($"CompanyApplication {applicationId} is not in status SUBMITTED");
        }

        if (string.IsNullOrWhiteSpace(data.ParticipantDetails.Bpn))
        {
            throw new ConflictException("BusinessPartnerNumber is null");
        }

        var transferData = new ClearinghouseTransferData(
            data.ParticipantDetails,
            new IdentityDetails(decentralizedIdentifier, data.UniqueIds));

        await _clearinghouseService.TriggerCompanyDataPost(transferData, cancellationToken).ConfigureAwait(false);
    }

    public async Task ProcessEndClearinghouse(Guid applicationId, ClearinghouseResponseData data, CancellationToken cancellationToken)
    {
        var context = await _checklistService
            .VerifyChecklistEntryAndProcessSteps(
                applicationId,
                ApplicationChecklistEntryTypeId.CLEARING_HOUSE,
                new [] { ApplicationChecklistEntryStatusId.IN_PROGRESS },
                ProcessStepTypeId.END_CLEARING_HOUSE,
                processStepTypeIds: new [] { ProcessStepTypeId.CREATE_SELF_DESCRIPTION_LP })
            .ConfigureAwait(false);

        var declined = data.Status == ClearinghouseResponseStatus.DECLINE;

        _checklistService.FinalizeChecklistEntryAndProcessSteps(
            context,
            item =>
            {
                item.ApplicationChecklistEntryStatusId = 
                    declined
                        ? ApplicationChecklistEntryStatusId.FAILED
                        : ApplicationChecklistEntryStatusId.DONE;
                item.Comment = data.Message;
            },
            declined
                ? null
                : new [] { ProcessStepTypeId.CREATE_SELF_DESCRIPTION_LP });
    }
}
