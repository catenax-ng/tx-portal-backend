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

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Org.Eclipse.TractusX.Portal.Backend.PortalBackend.Migrations.Migrations
{
    public partial class CPLP2195AddMimeTypeToDocuments : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "mime_type",
                schema: "portal",
                table: "documents",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE portal.documents SET mime_type = 'image/jpg' where LOWER(document_name) LIKE '%.jpg'");
            migrationBuilder.Sql("UPDATE portal.documents SET mime_type = 'image/jpeg' where LOWER(document_name) LIKE '%.jpeg'");
            migrationBuilder.Sql("UPDATE portal.documents SET mime_type = 'image/png' where LOWER(document_name) LIKE '%.png'");
            migrationBuilder.Sql("UPDATE portal.documents SET mime_type = 'image/gif' where LOWER(document_name) LIKE '%.gif'");
            migrationBuilder.Sql("UPDATE portal.documents SET mime_type = 'image/svg+xml' where LOWER(document_name) LIKE '%.svg'");
            migrationBuilder.Sql("UPDATE portal.documents SET mime_type = 'image/tiff' where LOWER(document_name) LIKE '%.tif'");
            migrationBuilder.Sql("UPDATE portal.documents SET mime_type = 'image/tiff' where LOWER(document_name) LIKE '%.tiff'");
            migrationBuilder.Sql("UPDATE portal.documents SET mime_type = 'application/pdf' where LOWER(document_name) LIKE '%.pdf'");
            migrationBuilder.Sql("UPDATE portal.documents SET mime_type = 'application/json' where LOWER(document_name) LIKE '%.json'");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "mime_type",
                schema: "portal",
                table: "documents");
        }
    }
}
