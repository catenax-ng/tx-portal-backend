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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Org.Eclipse.TractusX.Portal.Backend.ApplicationActivation.Library;
using Org.Eclipse.TractusX.Portal.Backend.Checklist.Library;
using Org.Eclipse.TractusX.Portal.Backend.Framework.Async;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Repositories;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Entities;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;
using System.Runtime.CompilerServices;
using System.Collections.Immutable;

namespace Org.Eclipse.TractusX.Portal.Backend.Checklist.Worker;

/// <summary>
/// Service that checks if there are open/pending tasks of a checklist and executes them.
/// </summary>
public class ChecklistExecutionService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<ChecklistExecutionService> _logger;

    /// <summary>
    /// Creates a new instance of <see cref="ChecklistExecutionService"/>
    /// </summary>
    /// <param name="serviceScopeFactory">access to the services</param>
    /// <param name="logger">the logger</param>
    public ChecklistExecutionService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<ChecklistExecutionService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    private static readonly IEnumerable<ProcessStepTypeId> _automaticProcessStepTypeIds = new [] {
        ProcessStepTypeId.CREATE_BUSINESS_PARTNER_NUMBER_PULL,
        ProcessStepTypeId.CREATE_IDENTITY_WALLET,
        ProcessStepTypeId.START_CLEARING_HOUSE,
        ProcessStepTypeId.CREATE_SELF_DESCRIPTION_LP
    }.ToImmutableArray();

    /// <summary>
    /// Handles the checklist processing
    /// </summary>
    /// <param name="stoppingToken">Cancellation Token</param>
    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var outerLoopScope = _serviceScopeFactory.CreateScope();
        var outerLoopRepositories = outerLoopScope.ServiceProvider.GetRequiredService<IPortalRepositories>();

        using var checklistServiceScope = outerLoopScope.ServiceProvider.CreateScope();
        var checklistService = checklistServiceScope.ServiceProvider.GetRequiredService<IChecklistService>();
        var applicationActivation = checklistServiceScope.ServiceProvider.GetRequiredService<IApplicationActivationService>();
        var checklistCreationService = checklistServiceScope.ServiceProvider.GetRequiredService<IChecklistCreationService>();
        var checklistRepositories = checklistServiceScope.ServiceProvider.GetRequiredService<IPortalRepositories>();

        if (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var checklistEntryData = outerLoopRepositories.GetInstance<IApplicationChecklistRepository>().GetChecklistProcessStepData();
                await foreach (var entryData in checklistEntryData.WithCancellation(stoppingToken).ConfigureAwait(false))
                {
                    var checklist = await HandleChecklistProcessing(entryData, checklistCreationService, checklistService, checklistRepositories, stoppingToken).ConfigureAwait(false);
                    await HandleApplicationActivation(entryData.ApplicationId, checklist, applicationActivation, checklistRepositories).ConfigureAwait(false);
                }
                _logger.LogInformation("Processed checklist items");
            }
            catch (Exception ex)
            {
                Environment.ExitCode = 1;
                _logger.LogError("Checklist processing failed with following Exception {ExceptionMessage}", ex.Message);
            }
        }
    }

    private static async Task<IEnumerable<(ApplicationChecklistEntryTypeId EntryTypeId, ApplicationChecklistEntryStatusId EntryStatusId)>> HandleChecklistProcessing(
        (Guid ApplicationId, IEnumerable<(ApplicationChecklistEntryTypeId TypeId, ApplicationChecklistEntryStatusId StatusId)> Checklist, IEnumerable<ProcessStep> ProcessSteps) entryData,
        IChecklistCreationService checklistCreationService,
        IChecklistService checklistService,
        IPortalRepositories checklistRepositories,
        CancellationToken stoppingToken)
    {
        var (applicationId, checklistEntries, processSteps) = entryData;
        if (Enum.GetValues<ApplicationChecklistEntryTypeId>().Length != checklistEntries.Count())
        {
            var missingChecklistEntryTypes = Enum.GetValues<ApplicationChecklistEntryTypeId>().Except(checklistEntries.Select(entry => entry.TypeId));

            var createdEntries = (await checklistCreationService
                .CreateMissingChecklistItems(applicationId, missingChecklistEntryTypes).ConfigureAwait(false)).ToList();
            checklistEntries = checklistEntries.Concat(createdEntries);

            var newSteps = checklistCreationService
                .CreateInitialProcessSteps(applicationId, createdEntries).ToList();
            processSteps = processSteps.Concat(newSteps.IntersectBy(_automaticProcessStepTypeIds, processStep => processStep.ProcessStepTypeId));

            await checklistRepositories.SaveAsync().ConfigureAwait(false);
            checklistRepositories.Clear();
        }
        var checklist = checklistEntries.ToDictionary(entry => entry.TypeId, entry => entry.StatusId);

        await foreach (var (typeId, statusId, processed) in checklistService.ProcessChecklist(applicationId, checklistEntries, processSteps, stoppingToken).WithCancellation(stoppingToken).ConfigureAwait(false))
        {
            if (processed)
            {
                await checklistRepositories.SaveAsync().ConfigureAwait(false);
                checklistRepositories.Clear();
            }
            checklist[typeId] = statusId;
        }
        return checklist.Select(entry => (entry.Key, entry.Value));
    }

    private async Task HandleApplicationActivation(Guid applicationId, IEnumerable<(ApplicationChecklistEntryTypeId TypeId, ApplicationChecklistEntryStatusId StatusId)> checklistEntries,
        IApplicationActivationService applicationActivation, IPortalRepositories checklistRepositories)
    {
        if (checklistEntries.All(x => x.StatusId == ApplicationChecklistEntryStatusId.DONE))
        {
            try
            {
                await applicationActivation.HandleApplicationActivation(applicationId).ConfigureAwait(false);
                await checklistRepositories.SaveAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError("Application activation for application {ApplicationId} failed with error {ErrorMessage}",
                    applicationId, ex.ToString());
            }
            checklistRepositories.Clear();
        }
    }
}
