using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.References.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedProvincesCities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // استان‌های ایران (31 استان)
            migrationBuilder.InsertData(
                schema: "references",
                table: "provinces",
                columns: new[] { "Name", "Slug", "SortOrder", "IsActive" },
                values: new object[,]
                {
                    { "تهران", "tehran", 1, true },
                    { "اصفهان", "isfahan", 2, true },
                    { "خراسان رضوی", "khorasan-razavi", 3, true },
                    { "فارس", "fars", 4, true },
                    { "خوزستان", "khuzestan", 5, true },
                    { "آذربایجان شرقی", "azerbaijan-east", 6, true },
                    { "مازندران", "mazandaran", 7, true },
                    { "کرمان", "kerman", 8, true },
                    { "آذربایجان غربی", "azerbaijan-west", 9, true },
                    { "گیلان", "gilan", 10, true },
                    { "کرمانشاه", "kermanshah", 11, true },
                    { "هرمزگان", "hormozgan", 12, true },
                    { "لرستان", "lorestan", 13, true },
                    { "مرکزی", "markazi", 14, true },
                    { "سیستان و بلوچستان", "sistan-baluchestan", 15, true },
                    { "کردستان", "kurdistan", 16, true },
                    { "همدان", "hamadan", 17, true },
                    { "چهارمحال و بختیاری", "chaharmahal-bakhtiari", 18, true },
                    { "قزوین", "qazvin", 19, true },
                    { "اردبیل", "ardabil", 20, true },
                    { "یزد", "yazd", 21, true },
                    { "قم", "qom", 22, true },
                    { "گلستان", "golestan", 23, true },
                    { "زنجان", "zanjan", 24, true },
                    { "بوشهر", "bushehr", 25, true },
                    { "سمنان", "semnan", 26, true },
                    { "ایلام", "ilam", 27, true },
                    { "کهگیلویه و بویراحمد", "kohgiluyeh-boyer-ahmad", 28, true },
                    { "خراسان شمالی", "khorasan-north", 29, true },
                    { "خراسان جنوبی", "khorasan-south", 30, true },
                    { "البرز", "alborz", 31, true }
                });

            // شهرهای استان تهران
            migrationBuilder.Sql(@"
                INSERT INTO references.cities (""Name"", ""Slug"", ""ProvinceId"", ""SortOrder"", ""IsActive"")
                SELECT 'تهران', 'tehran', p.""Id"", 1, true
                FROM references.provinces p WHERE p.""Slug"" = 'tehran';

                INSERT INTO references.cities (""Name"", ""Slug"", ""ProvinceId"", ""SortOrder"", ""IsActive"")
                SELECT 'کرج', 'karaj', p.""Id"", 1, true
                FROM references.provinces p WHERE p.""Slug"" = 'alborz';

                INSERT INTO references.cities (""Name"", ""Slug"", ""ProvinceId"", ""SortOrder"", ""IsActive"")
                SELECT 'اصفهان', 'isfahan', p.""Id"", 1, true
                FROM references.provinces p WHERE p.""Slug"" = 'isfahan';

                INSERT INTO references.cities (""Name"", ""Slug"", ""ProvinceId"", ""SortOrder"", ""IsActive"")
                SELECT 'مشهد', 'mashhad', p.""Id"", 1, true
                FROM references.provinces p WHERE p.""Slug"" = 'khorasan-razavi';

                INSERT INTO references.cities (""Name"", ""Slug"", ""ProvinceId"", ""SortOrder"", ""IsActive"")
                SELECT 'شیراز', 'shiraz', p.""Id"", 1, true
                FROM references.provinces p WHERE p.""Slug"" = 'fars';

                INSERT INTO references.cities (""Name"", ""Slug"", ""ProvinceId"", ""SortOrder"", ""IsActive"")
                SELECT 'اهواز', 'ahvaz', p.""Id"", 1, true
                FROM references.provinces p WHERE p.""Slug"" = 'khuzestan';

                INSERT INTO references.cities (""Name"", ""Slug"", ""ProvinceId"", ""SortOrder"", ""IsActive"")
                SELECT 'تبریز', 'tabriz', p.""Id"", 1, true
                FROM references.provinces p WHERE p.""Slug"" = 'azerbaijan-east';

                INSERT INTO references.cities (""Name"", ""Slug"", ""ProvinceId"", ""SortOrder"", ""IsActive"")
                SELECT 'رشت', 'rasht', p.""Id"", 1, true
                FROM references.provinces p WHERE p.""Slug"" = 'gilan';

                INSERT INTO references.cities (""Name"", ""Slug"", ""ProvinceId"", ""SortOrder"", ""IsActive"")
                SELECT 'کرمان', 'kerman', p.""Id"", 1, true
                FROM references.provinces p WHERE p.""Slug"" = 'kerman';

                INSERT INTO references.cities (""Name"", ""Slug"", ""ProvinceId"", ""SortOrder"", ""IsActive"")
                SELECT 'ارومیه', 'urmia', p.""Id"", 1, true
                FROM references.provinces p WHERE p.""Slug"" = 'azerbaijan-west';

                INSERT INTO references.cities (""Name"", ""Slug"", ""ProvinceId"", ""SortOrder"", ""IsActive"")
                SELECT 'ساری', 'sari', p.""Id"", 1, true
                FROM references.provinces p WHERE p.""Slug"" = 'mazandaran';

                INSERT INTO references.cities (""Name"", ""Slug"", ""ProvinceId"", ""SortOrder"", ""IsActive"")
                SELECT 'کرمانشاه', 'kermanshah', p.""Id"", 1, true
                FROM references.provinces p WHERE p.""Slug"" = 'kermanshah';

                INSERT INTO references.cities (""Name"", ""Slug"", ""ProvinceId"", ""SortOrder"", ""IsActive"")
                SELECT 'بندرعباس', 'bandar-abbas', p.""Id"", 1, true
                FROM references.provinces p WHERE p.""Slug"" = 'hormozgan';

                INSERT INTO references.cities (""Name"", ""Slug"", ""ProvinceId"", ""SortOrder"", ""IsActive"")
                SELECT 'خرم‌آباد', 'khorramabad', p.""Id"", 1, true
                FROM references.provinces p WHERE p.""Slug"" = 'lorestan';

                INSERT INTO references.cities (""Name"", ""Slug"", ""ProvinceId"", ""SortOrder"", ""IsActive"")
                SELECT 'اراک', 'arak', p.""Id"", 1, true
                FROM references.provinces p WHERE p.""Slug"" = 'markazi';

                INSERT INTO references.cities (""Name"", ""Slug"", ""ProvinceId"", ""SortOrder"", ""IsActive"")
                SELECT 'زاهدان', 'zahedan', p.""Id"", 1, true
                FROM references.provinces p WHERE p.""Slug"" = 'sistan-baluchestan';

                INSERT INTO references.cities (""Name"", ""Slug"", ""ProvinceId"", ""SortOrder"", ""IsActive"")
                SELECT 'سنندج', 'sanandaj', p.""Id"", 1, true
                FROM references.provinces p WHERE p.""Slug"" = 'kurdistan';

                INSERT INTO references.cities (""Name"", ""Slug"", ""ProvinceId"", ""SortOrder"", ""IsActive"")
                SELECT 'همدان', 'hamadan', p.""Id"", 1, true
                FROM references.provinces p WHERE p.""Slug"" = 'hamadan';

                INSERT INTO references.cities (""Name"", ""Slug"", ""ProvinceId"", ""SortOrder"", ""IsActive"")
                SELECT 'شهرکرد', 'shahrekord', p.""Id"", 1, true
                FROM references.provinces p WHERE p.""Slug"" = 'chaharmahal-bakhtiari';

                INSERT INTO references.cities (""Name"", ""Slug"", ""ProvinceId"", ""SortOrder"", ""IsActive"")
                SELECT 'قزوین', 'qazvin', p.""Id"", 1, true
                FROM references.provinces p WHERE p.""Slug"" = 'qazvin';

                INSERT INTO references.cities (""Name"", ""Slug"", ""ProvinceId"", ""SortOrder"", ""IsActive"")
                SELECT 'اردبیل', 'ardabil', p.""Id"", 1, true
                FROM references.provinces p WHERE p.""Slug"" = 'ardabil';

                INSERT INTO references.cities (""Name"", ""Slug"", ""ProvinceId"", ""SortOrder"", ""IsActive"")
                SELECT 'یزد', 'yazd', p.""Id"", 1, true
                FROM references.provinces p WHERE p.""Slug"" = 'yazd';

                INSERT INTO references.cities (""Name"", ""Slug"", ""ProvinceId"", ""SortOrder"", ""IsActive"")
                SELECT 'قم', 'qom', p.""Id"", 1, true
                FROM references.provinces p WHERE p.""Slug"" = 'qom';

                INSERT INTO references.cities (""Name"", ""Slug"", ""ProvinceId"", ""SortOrder"", ""IsActive"")
                SELECT 'گرگان', 'gorgan', p.""Id"", 1, true
                FROM references.provinces p WHERE p.""Slug"" = 'golestan';

                INSERT INTO references.cities (""Name"", ""Slug"", ""ProvinceId"", ""SortOrder"", ""IsActive"")
                SELECT 'زنجان', 'zanjan', p.""Id"", 1, true
                FROM references.provinces p WHERE p.""Slug"" = 'zanjan';

                INSERT INTO references.cities (""Name"", ""Slug"", ""ProvinceId"", ""SortOrder"", ""IsActive"")
                SELECT 'بوشهر', 'bushehr', p.""Id"", 1, true
                FROM references.provinces p WHERE p.""Slug"" = 'bushehr';

                INSERT INTO references.cities (""Name"", ""Slug"", ""ProvinceId"", ""SortOrder"", ""IsActive"")
                SELECT 'سمنان', 'semnan', p.""Id"", 1, true
                FROM references.provinces p WHERE p.""Slug"" = 'semnan';

                INSERT INTO references.cities (""Name"", ""Slug"", ""ProvinceId"", ""SortOrder"", ""IsActive"")
                SELECT 'ایلام', 'ilam', p.""Id"", 1, true
                FROM references.provinces p WHERE p.""Slug"" = 'ilam';

                INSERT INTO references.cities (""Name"", ""Slug"", ""ProvinceId"", ""SortOrder"", ""IsActive"")
                SELECT 'یاسوج', 'yasuj', p.""Id"", 1, true
                FROM references.provinces p WHERE p.""Slug"" = 'kohgiluyeh-boyer-ahmad';

                INSERT INTO references.cities (""Name"", ""Slug"", ""ProvinceId"", ""SortOrder"", ""IsActive"")
                SELECT 'بجنورد', 'bojnurd', p.""Id"", 1, true
                FROM references.provinces p WHERE p.""Slug"" = 'khorasan-north';

                INSERT INTO references.cities (""Name"", ""Slug"", ""ProvinceId"", ""SortOrder"", ""IsActive"")
                SELECT 'بیرجند', 'birjand', p.""Id"", 1, true
                FROM references.provinces p WHERE p.""Slug"" = 'khorasan-south';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // حذف شهرها
            migrationBuilder.Sql("DELETE FROM references.cities;");
            
            // حذف استان‌ها
            migrationBuilder.Sql("DELETE FROM references.provinces;");
        }
    }
}
