using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrowdFunding.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EncryptSigningKeysAtRest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Schema half of the at-rest encryption change. Existing rows keep their plain base64
            // PKCS#8 value under the new name; EfSigningKeyStore re-encrypts them on the first
            // warm-up that has SigningKeyEncryption:Key configured (a SQL migration cannot, since
            // the encryption key deliberately never reaches the database).
            migrationBuilder.RenameColumn(
                name: "PrivateKeyPkcs8Base64",
                table: "identity_signing_keys",
                newName: "ProtectedPrivateKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ProtectedPrivateKey",
                table: "identity_signing_keys",
                newName: "PrivateKeyPkcs8Base64");
        }
    }
}
