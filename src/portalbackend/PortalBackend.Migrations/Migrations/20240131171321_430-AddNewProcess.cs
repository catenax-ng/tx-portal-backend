/********************************************************************************
 * Copyright (c) 2021, 2024 Contributors to the Eclipse Foundation
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

using Microsoft.EntityFrameworkCore.Migrations;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Entities;

#nullable disable

namespace Org.Eclipse.TractusX.Portal.Backend.PortalBackend.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class _430AddNewProcess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "portal",
                table: "process_step_types",
                columns: new[] { "id", "label" },
                values: new object[] { 300, "SYNC_COMPANY_USER_IDP" });

            migrationBuilder.InsertData(
                schema: "portal",
                table: "process_types",
                columns: new[] { "id", "label" },
                values: new object[] { 5, "SYNC_COMPANY_USER_IDP" });

            migrationBuilder.Sql(@"INSERT INTO portal.processes (id, process_type_id, lock_expiry_date, version) VALUES ('487c2a3a-2975-4718-8408-846416457f1b', 5, null, 'deadbeef-dead-beef-dead-beefdeadbeef');");
            migrationBuilder.Sql(@"INSERT INTO portal.process_steps (id, process_step_type_id, process_step_status_id, date_created, date_last_changed, process_id, message) values ('2c477ca1-6b92-4f11-8288-6aea5d1bb7ad', 300, 1, CURRENT_TIMESTAMP, null, '487c2a3a-2975-4718-8408-846416457f1b', null);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM portal.processes WHERE process_type_id = 5");
            migrationBuilder.Sql("DELETE FROM portal.process_steps WHERE process_step_type_id = 300");

            migrationBuilder.DeleteData(
                schema: "portal",
                table: "process_step_types",
                keyColumn: "id",
                keyValue: 300);

            migrationBuilder.DeleteData(
                schema: "portal",
                table: "process_types",
                keyColumn: "id",
                keyValue: 5);
        }
    }
}
