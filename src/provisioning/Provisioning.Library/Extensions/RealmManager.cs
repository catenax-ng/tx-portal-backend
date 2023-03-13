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

using Org.Eclipse.TractusX.Portal.Backend.Keycloak.Library;
using Org.Eclipse.TractusX.Portal.Backend.Keycloak.Library.Models.RealmsAdmin;
using System.Text.Json;
using Org.Eclipse.TractusX.Portal.Backend.Keycloak.Library.Models.AuthenticationManagement;
namespace Org.Eclipse.TractusX.Portal.Backend.Provisioning.Library;

public partial class ProvisioningManager
{
    private Task CreateSharedRealmAsync(KeycloakClient keycloak, string realm, string displayName)
    {
        var newRealm = CloneRealm(_Settings.SharedRealm);
        newRealm.Id = realm;
        newRealm._Realm = realm;
        newRealm.DisplayName = displayName;
        return keycloak.ImportRealmAsync(realm, newRealm);
    }

    private static async ValueTask UpdateSharedRealmAsync(KeycloakClient keycloak, string alias, string displayName)
    {
        var realm = await keycloak.GetRealmAsync(alias).ConfigureAwait(false);
        realm.DisplayName = displayName;
        await keycloak.UpdateRealmAsync(alias, realm).ConfigureAwait(false);
    }


    public static async ValueTask UpdateSharedRealmAuthenticationAsync(KeycloakClient keycloak, string alias)
    {
        string customFlow = "Custom_browser";
        string originalFlow = "browser";
        string customValidatorProviderId = "cus-auth-form";

        IDictionary<string, object> customParam = new Dictionary<string,object>();
        customParam.Add("newName",customFlow);
        await keycloak.duplicateFlow(alias, originalFlow,customParam);

        customParam = new Dictionary<string,object>();
        customParam.Add("provider",customValidatorProviderId);
        string parentForm =customFlow+" forms";
        await keycloak.addExecution(alias, parentForm,customParam);

        List<AuthenticationFlowExecution> authticationFlows= await keycloak.GetAuthenticationFlow(alias, customFlow);

        if(authticationFlows != null && authticationFlows.ElementAt(0) != null){
        string defaultUsernameExecutionId="";
        AuthenticationFlowExecution? customExecution =null;
        
            foreach(var authticationFlow in authticationFlows)
            {
                if(authticationFlow.ProviderId ==customValidatorProviderId){
                    customExecution = authticationFlow;
                }else if (authticationFlow.ProviderId=="auth-username-password-form"){
                    defaultUsernameExecutionId= authticationFlow.Id;
                }
            }

            await keycloak.DeleteExecution(alias,defaultUsernameExecutionId);
            if(customExecution != null){
                await keycloak.updatePriority(alias, customExecution.Id,customParam);
                customExecution.Requirement="REQUIRED";
                customExecution.Level=1;
                customExecution.Index=0;

                await keycloak.UpdateExecution(alias,customFlow,customExecution);
            }
        }
        var realm = await keycloak.GetRealmAsync(alias).ConfigureAwait(false);
        realm.BrowserFlow= customFlow;
        await keycloak.UpdateRealmAsync(alias, realm).ConfigureAwait(false);    
    }
    private static async ValueTask SetSharedRealmStatusAsync(KeycloakClient keycloak, string alias, bool enabled)
    {
        var realm = await keycloak.GetRealmAsync(alias).ConfigureAwait(false);
        realm.Enabled = enabled;
        await keycloak.UpdateRealmAsync(alias, realm).ConfigureAwait(false);
    }

    private static Realm CloneRealm(Realm realm) =>
        JsonSerializer.Deserialize<Realm>(JsonSerializer.Serialize(realm))!;
}
