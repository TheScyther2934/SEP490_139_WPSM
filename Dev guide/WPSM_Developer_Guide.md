# Hướng dẫn phát triển WPSM

Tài liệu này giúp thành viên mới chạy dự án, hiểu luồng từ giao diện đến SQL Server, biết file nào thuộc feature mình phụ trách và phối hợp thay đổi mà không làm lệch database hoặc API. Solution hiện tại dùng React trong `src/Client`, ASP.NET Core API và các project `Application`, `Domain`, `Infrastructure` trên .NET 8.

> **Tình trạng hiện tại:** solution đã có luồng database-backed cho Iteration 1 và repository đã chia theo feature. Một số tính năng phía sau Iteration 1 và một số tích hợp được mô tả trong Report 4 chưa được triển khai. Hãy kiểm tra Report 2 để xác định phạm vi iteration, Report 3 để xác định yêu cầu chức năng, Report 4 để hiểu thiết kế mục tiêu và thư mục `Screens` để xem đặc tả giao diện.

## 1. Bắt đầu làm việc với dự án

### 1.1 Công cụ cần có

- .NET 8 SDK.
- Node.js và npm để chạy React/Vite.
- SQL Server hoặc SQL Server LocalDB. SQL Server là nơi lưu dữ liệu; LocalDB là cách chạy SQL Server cục bộ trên máy Windows cho phát triển.
- SQL Server Management Studio (SSMS) hoặc công cụ tương đương để chạy script và xem dữ liệu.
- Git và IDE/editor hỗ trợ .NET.

### 1.2 Khởi tạo database

Nhóm thống nhất dùng cùng schema và bộ dữ liệu khởi tạo. Thông thường mỗi thành viên chạy database riêng trên máy mình bằng cùng script; không bắt buộc cả nhóm dùng một server online chung.

**Không có project SQL Database riêng để bấm Run trong solution.** `src/Infrastructure` là class library, không tự tạo database. Phải chạy schema SQL trong SSMS trước. Seeder là bước khác: nó chạy bên trong API khi API khởi động, và hiện mặc định đang tắt (`Wpsm:SeedDevelopmentData=false`).

1. Mở `Database/WPSM_DB_Schema.sql` bằng SSMS và chạy trên SQL Server. Script tạo database `WPSM_DB` nếu chưa có và tạo các bảng/ràng buộc/index hiện được định nghĩa trong đó.
2. Connection mặc định của Development nằm trong `src/API/appsettings.Development.json` và dùng `(localdb)\MSSQLLocalDB`. Nếu dùng SQL Server khác, mỗi thành viên đặt connection string riêng bằng User Secrets, không sửa connection string cá nhân vào file được commit:

   ```powershell
   dotnet user-secrets set "ConnectionStrings:WpsmDb" "<connection-string-cua-may-ban>" --project src/API/API.csproj
   ```

3. Nếu nhóm dùng seeder hiện tại, bật seeder và cấu hình email/mật khẩu mà seeder sẽ dùng để tạo hai tài khoản ứng dụng Admin và Manager. Đây là tài khoản đăng nhập trên giao diện React, được lưu trong bảng `Users`; **không phải tài khoản Windows/SQL Server và cũng không phải tài khoản User Secrets**. User Secrets chỉ giữ cấu hình bootstrap này bên ngoài source code.

   Nếu mỗi thành viên có database LocalDB riêng, cả nhóm nên dùng cùng email/mật khẩu development để ai cũng đăng nhập được theo cùng hướng dẫn (mỗi database riêng sẽ có bản ghi tài khoản riêng). Nếu cả nhóm dùng một database chung, phải thống nhất một cặp email/mật khẩu Admin và một cặp Manager; không tự đặt khác nhau trên từng máy. Seeder không đổi mật khẩu của tài khoản đã tồn tại.

   Thay `<mat-khau-dev-rieng>` bằng mật khẩu development thật của nhóm, tối thiểu 12 ký tự; không gõ nguyên dấu `< >`:

   ```powershell
   dotnet user-secrets set "Wpsm:SeedDevelopmentData" "true" --project src/API/API.csproj
   dotnet user-secrets set "Wpsm:Seed:Admin:Email" "admin@wpsm.local" --project src/API/API.csproj
   dotnet user-secrets set "Wpsm:Seed:Admin:Password" "<mat-khau-dev-rieng>" --project src/API/API.csproj
   dotnet user-secrets set "Wpsm:Seed:Manager:Email" "manager@wpsm.local" --project src/API/API.csproj
   dotnet user-secrets set "Wpsm:Seed:Manager:Password" "<mat-khau-dev-rieng>" --project src/API/API.csproj
   ```

   `UserSecretsId` đã có trong `src/API/API.csproj`, nên không cần chạy `user-secrets init`. Khi API chạy ở Development, seeder đọc các giá trị này, băm mật khẩu rồi tạo bản ghi trong `Users`. Đặt `Wpsm:SeedDevelopmentData` thành `false` nếu nhóm seed tài khoản bằng cách khác.

4. Seeder chạy khi API khởi động trong môi trường Development và tạo tài khoản Admin/Manager, sảnh, ca tiệc, Catalog, PriceFrames và policy cơ bản. Seeder kiểm tra bản ghi đã có trước khi thêm; nếu đổi password của tài khoản đã được seed, cần cập nhật có chủ ý trong database hoặc xóa database dev cục bộ rồi tạo lại.

`WPSM_DB_Schema.sql` hiện là script schema. Dữ liệu khởi tạo hiện nằm trong `src/API/DevelopmentDatabaseSeeder.cs`, không phải trong SQL script. Nếu nhóm muốn quy trình một script duy nhất, cần thống nhất rồi chuyển seed dữ liệu cố định vào script hoặc duy trì một `WPSM_DB_Seed.sql` riêng. Không đưa mật khẩu thật vào script dùng chung.

**Lưu ý chia sẻ qua Git:** schema SQL hiện nằm trong repository tại `Database/WPSM_DB_Schema.sql`. Các report trong `../Docs` và screen design trong `../Screens` vẫn nằm ngoài Git root; thành viên clone riêng repository sẽ không tự nhận được các tài liệu đó. Nhóm cần cung cấp report/screen design bằng workspace hoặc đưa tài liệu cần thiết vào Git. Không giả định đường dẫn `../Docs` hoặc `../Screens` có sẵn trên máy thành viên.

### 1.3 Chạy ứng dụng

Mở hai terminal tại thư mục solution `SEP490_139_WPSM`.

Terminal 1 – API:

```powershell
dotnet run --project src/API/API.csproj --launch-profile http
```

Terminal 2 – React:

```powershell
cd src/Client
npm ci
npm run dev
```

Mở `http://localhost:5173`. Vite chuyển tiếp `/api/wpsm/*` đến API ở `http://localhost:5128`. Giữ cả hai tiến trình chạy khi thao tác trên giao diện. Để tạo bản build được API phục vụ, chạy `npm run build` trong `src/Client`; output nằm trong `src/API/wwwroot`.

### 1.4 Kiểm tra nhanh trước khi nhận feature

- `dotnet build WPSM.sln --no-restore -m:1` build thành công.
- API khởi động mà không báo thiếu connection string hoặc lỗi kết nối SQL Server.
- Đăng nhập bằng tài khoản dev vừa seed.
- Kiểm tra role chuyển đúng trang; thử một luồng đọc dữ liệu và một luồng ghi dữ liệu trong phạm vi được giao.
- Nếu API trả 503, kiểm tra SQL Server đang chạy, database đã tạo, connection string đúng và user có quyền.
- Nếu API trả 401/403, kiểm tra đăng nhập, refresh cookie và role; build thành công không xác nhận các điều kiện này.

## 2. Kiến trúc và luồng tương tác giữa project

```mermaid
flowchart LR
    U[Trình duyệt] -->|React/Vite| WEB[src/Client · React]
    WEB -->|/api/wpsm/* proxy khi dev| API[WPSM.API · endpoints, auth, DI]
    API -->|feature interface + DTO| APP[WPSM.Application]
    APP -->|không phụ thuộc EF/SQL| DOM[WPSM.Domain]
    API -->|đăng ký implementation tại composition root| INF[WPSM.Infrastructure]
    INF -->|EF Core DbContext/entities| DB[(SQL Server · WPSM_DB)]
    API -.->|bootstrap reader tổng hợp| INF
```

Luồng đọc/ghi thường gặp:

1. Thành viên tạo hoặc chỉnh component/page trong `src/Client/src`.
2. React gọi `/api/wpsm/...` qua `src/Client/src/services` và `src/Client/src/api`; không gọi trực tiếp SQL Server và không tham chiếu `Infrastructure`.
3. Vite proxy request `/api` sang API khi phát triển; khi deploy, API phục vụ bundle React trong `src/API/wwwroot`.
4. `src/API/Program.cs` định nghĩa API endpoint, authorization và đăng ký implementation qua Dependency Injection.
5. Endpoint gọi interface feature nằm trong `Application/Persistence`.
6. Implementation tại `Infrastructure/Persistence` dùng `WpsmDbContext`/EF Core để đọc hoặc ghi bảng.
7. DTO trả về API rồi JavaScript cập nhật màn hình.

**Quy tắc phụ thuộc:** React chỉ biết HTTP/API; Application chứa contract dùng chung; Infrastructure chứa chi tiết SQL/EF; API là composition root nối interface với implementation. Không để component React hoặc API endpoint tự truy vấn `WpsmDbContext`.

## 3. Ý nghĩa project và folder

### `src/Client`

Giao diện React, được phát triển bằng Vite và Material UI.

- `src/pages`: màn hình theo vai trò Admin, Manager, Customer và xác thực.
- `src/layouts`, `src/routes`: bố cục, route và kiểm tra role.
- `src/services`, `src/api`: gọi REST API và quản lý đường dẫn endpoint.
- `src/store`: trạng thái đăng nhập và dữ liệu giao diện bằng Redux Toolkit.
- `vite.config.js`: dev server ở cổng 5173, proxy API ở cổng 5128; build vào `src/API/wwwroot`.

### `src/API`

ASP.NET Core Web API, authentication/authorization và nơi khởi tạo ứng dụng.

- `Program.cs`: đọc connection string, đăng ký `DbContext`, repositories, hasher, JWT authentication, middleware lỗi và endpoints `/api/wpsm/*`; đồng thời phục vụ React bundle khi đã build. Refresh token được gửi bằng HttpOnly cookie.
- `DevelopmentDatabaseSeeder.cs`: tạo tài khoản dev và dữ liệu tham chiếu khi bật seed trong Development.
- `appsettings.Development.json`: connection mặc định LocalDB và cờ seed mặc định tắt.
- `Properties/launchSettings.json`: profile/port chạy local.
- `WpsmPortalStore.cs`: store in-memory cũ; code hiện tại không nên dùng file này cho chức năng database-backed. Các luồng đang dùng repository EF trong Infrastructure.

### `src/Application`

Contract giữa API và persistence, cùng DTO/command cho các luồng hiện tại.

- `Persistence/WpsmPortalContracts.cs`: DTO/command/response như `WeddingProjectView`, `CatalogItemView`, `PolicyCommand`, `ProfileCommand`.
- `Persistence/IProjectRepository.cs`: đọc/tạo dự án, ghi nhận biên nhận NRD.
- `Persistence/ISpecificationRoadmapRepository.cs`: sửa đặc tả, freeze baseline và đọc roadmap.
- `Persistence/IConfigurationRepository.cs`: Catalog, PriceFrames, Policies.
- `Persistence/IAccountProfileRepository.cs`: tra cứu tài khoản, đổi password và hồ sơ.
- `Persistence/IWpsmPortalSnapshotReader.cs`: contract đọc gói dữ liệu bootstrap cho giao diện.
- `Features/<Feature>/` là vị trí dự kiến cho use case và DTO riêng của feature khi nhóm bổ sung Application handlers. `Behaviors/` dành cho pipeline behavior nếu nhóm triển khai pattern đó.
- `Features/`, `Behaviors/`, `Common/` hiện là folder khung; không tạo DTO chung ở folder top-level riêng hoặc interface trùng lặp. Contract persistence hiện dùng đặt trong `Persistence/`.

### `src/Domain`

Nơi đặt quy tắc nghiệp vụ thuần, trạng thái và value objects, không phụ thuộc database, HTTP hay giao diện. Hiện project còn trống/placeholder; không nhầm với entity EF đang nằm trong Infrastructure. Khi bổ sung quy tắc phức tạp (ví dụ chuyển trạng thái Change Request hoặc tính surcharge), đưa logic thuần vào Domain thay vì nhồi thêm vào endpoint hoặc EF repository. Dùng `Services/` cho thuật toán nghiệp vụ thuần.

Các folder `Entities/`, `Enums/`, `Events/`, `Exceptions/`, `ValueObjects/`, `Services/` trong Domain hiện chỉ là folder khung. Tương tự, `Application/Behaviors`, `Common`, `Features` chưa có implementation đầy đủ. Folder chỉ tổ chức file; nó không tự chạy hay tự được DI sử dụng.

### `src/Infrastructure`

Chi tiết truy cập dữ liệu/EF Core và implementation của contract Application.

- `Persistence/WpsmDbContext.cs`: ánh xạ DbSet, quan hệ, cột, index và constraint với database-first schema.
- `Persistence/Entities`: EF entity sinh/ánh xạ bảng SQL. Thay đổi schema phải rà cả file này và DbContext.
- `Persistence/Repositories/ProjectRepository.cs`: thao tác dữ liệu dự án và NRD.
- `Persistence/Repositories/SpecificationRoadmapRepository.cs`: đặc tả, baseline freeze, roadmap.
- `Persistence/Repositories/ConfigurationRepository.cs`: Catalog, PriceFrames, Policies.
- `Persistence/Repositories/AccountProfileRepository.cs`: account/password/profile.
- `Persistence/WpsmPortalSnapshotReader.cs`: ghép dữ liệu từ các repository cho endpoint bootstrap; không thêm các thao tác ghi feature vào đây.
- `Persistence/ProjectReadModel.cs`: truy vấn/ánh xạ dữ liệu dự án sang DTO.
- `WpsmCategoryNames.cs`, `WpsmValueNormalizer.cs`: chuẩn hóa tên danh mục/giá trị dùng trong persistence.
- `Persistence/Repositories/` là vị trí duy nhất cho implementation repository EF; không tạo `Infrastructure/Repositories` bên ngoài `Persistence`.
- `Identity/`, `Extensions/`, `Files/`, `Email/`, `Pdf/`, `Jobs/` là các vùng hạ tầng dự kiến cho identity, đăng ký DI, lưu file, email, PDF và background jobs. Một số hiện mới là placeholder. Folder `Services/` chung đã bỏ để tránh mơ hồ về trách nhiệm.

### `Docs` và `Screens`

- `../Docs/WPSM_Report2_Project management plan.docx`: iteration và kế hoạch.
- `../Docs/WPSM_Report3_Software Requirement Specification.docx`: yêu cầu nghiệp vụ/use case.
- `../Docs/WPSM - Report 4 - Software Design Specification (3).docx`: tài liệu thiết kế mục tiêu; đối chiếu với quyết định React, API, auth và schema mới nhất khi dùng tài liệu này.
- `Database/WPSM_DB_Schema.sql`: schema SQL đang dùng làm cơ sở database-first và được theo dõi trong repository.
- `../Screens/<Role>/<Screen>/DESIGN.md`: trạng thái, trường dữ liệu và hành vi UI cho từng màn hình (thư mục này hiện cũng nằm ngoài Git root).

## 4. Chia repository theo feature

| Nhóm | Contract Application | Implementation Infrastructure | Phạm vi hiện tại |
|---|---|---|---|
| Dự án | `IProjectRepository` | `ProjectRepository` | Danh sách/chi tiết/tạo dự án, ghi nhận NRD |
| Đặc tả và roadmap | `ISpecificationRoadmapRepository` | `SpecificationRoadmapRepository` | Sửa đặc tả, freeze baseline, tạo/đọc roadmap |
| Cấu hình Admin | `IConfigurationRepository` | `ConfigurationRepository` | Catalog, PriceFrames, Policies |
| Tài khoản/hồ sơ | `IAccountProfileRepository` | `AccountProfileRepository` | Tra cứu account, đăng nhập hỗ trợ, password, profile |
| Yêu cầu thay đổi (FE-05) | `IChangeRequestRepository` | `ChangeRequestRepository` | Danh sách/form/chi tiết CR, thương lượng, phê duyệt hai bên, phụ lục, ngoại lệ phạt T-day |
| Lịch tài nguyên (FE-07) | `IResourceCalendarRepository` | `ResourceCalendarRepository` | Lịch sảnh/ca và tài nguyên, phát hiện xung đột GBR-09, đổi thiết bị/giờ, thu hồi, ngoại lệ Admin |
| Bootstrap màn hình | `IWpsmPortalSnapshotReader` | `WpsmPortalSnapshotReader` | Chỉ tổng hợp DTO cho UI; phụ thuộc các interface feature |

Khi bắt đầu feature mới, thêm contract hẹp cho feature thay vì nối thêm mọi method vào một repository chung. Giữ query/write liên quan trong cùng feature; chỉ tách interface mới khi có trách nhiệm riêng rõ ràng. Register implementation tương ứng trong `src/API/Program.cs`.

## 5. Quy trình trước và trong khi phát triển feature

### Trước khi code

1. Xác nhận feature thuộc iteration nào; đọc use case trong Report 3 và screen design liên quan.
2. Thống nhất acceptance criteria: ai được dùng, dữ liệu đầu vào/đầu ra, trạng thái trước/sau, lỗi mong đợi.
3. Xác định bảng SQL liên quan, contract API và interface/repository feature sẽ dùng.
4. Kiểm tra feature có thay schema không. Nếu có, giao một người điều phối schema; không để nhiều branch sửa độc lập cùng bảng/script.
5. Tạo branch từ nhánh nhóm đã thống nhất, ví dụ `feature/iteration1-project-baseline`.
6. Chia quyền sở hữu file. Tránh để nhiều thành viên cùng sửa `src/API/Program.cs`, `wpsm-api.js`, `WpsmDbContext.cs` hoặc SQL script trong cùng lúc; người phụ trách feature phối hợp với người giữ file composition/shared UI.

### Trong khi code

1. UI: sửa component/page phù hợp trong `src/Client/src`; giữ role routing và luồng API.
2. API: thêm endpoint mỏng, kiểm tra input và role; endpoint chỉ phụ thuộc contract feature.
3. Application: thêm/chỉnh DTO, command và interface trước khi Infrastructure implement; báo cho thành viên phụ thuộc khi contract đổi.
4. Infrastructure: thực hiện query/update EF trong repository của đúng feature; không trả EF entity trực tiếp ra API.
5. Domain/Application: đặt quy tắc nghiệp vụ độc lập với database ở lớp nghiệp vụ, không để UI quyết định quy tắc có ảnh hưởng dữ liệu.
6. Schema: cập nhật SQL script, DbContext và entity đồng bộ; seed chỉ thêm dữ liệu cần cho chạy/demo và phải tránh tạo trùng.
7. Bảo mật: không commit connection string/password thật; không trả `PasswordHash`; giới hạn quyền truy cập theo role và project ownership; file upload không đặt trong `wwwroot`.
8. Sau mỗi thay đổi contract, kiểm tra cùng người làm UI/API/Infrastructure để tên JSON, kiểu dữ liệu và status code khớp nhau.

### Trước khi mở PR/merge

- Build solution: `dotnet build WPSM.sln --no-restore -m:1`.
- Chạy luồng đã sửa trên database local có schema/seed; kiểm tra cả trường hợp hợp lệ và lỗi chính.
- Kiểm tra role trái phép, dữ liệu thiếu/trùng và ngày/tiền không hợp lệ nếu feature có liên quan.
- Rà `git diff` để chắc không commit `bin/`, `obj/`, secrets, dữ liệu cá nhân hoặc thay đổi không thuộc task.
- Cập nhật README/Report/design docs nếu route, schema, auth hoặc flow đã đổi.
- Trong mô tả PR ghi: use case, file/lớp đã sửa, schema/seed cần chạy, cách kiểm tra và giới hạn còn lại.

## 6. Quy tắc database-first và seed

SQL script là định nghĩa schema mà nhóm chạy trên SQL Server; `WpsmDbContext` và EF entities là mapping trong code. Không coi thay đổi trong DbContext tự động cập nhật database, vì hiện nhóm chưa dùng EF migrations làm workflow chính.

Khi thêm/sửa cột, bảng, constraint hoặc index:

1. Cập nhật `Database/WPSM_DB_Schema.sql` (idempotent, không drop dữ liệu tùy tiện).
2. Áp dụng thay đổi lên database dev sạch và kiểm tra script chạy lặp lại an toàn.
3. Cập nhật/scaffold lại `WpsmDbContext` và entity tương ứng trong Infrastructure; rà tên cột, nullability, precision, FK và computed columns.
4. Cập nhật seed nếu feature cần dữ liệu khởi tạo; bảo đảm chạy nhiều lần không tạo duplicate.
5. Ghi migration/setup note trong PR để thành viên khác cập nhật local database cùng cách.

Không sửa trực tiếp database của thành viên khác nếu chưa thống nhất. Không dùng dữ liệu khách hàng thật làm seed. Nếu nhóm sau này chuyển qua EF migrations, cần chốt migration là source of truth thay vì để hai workflow schema cạnh tranh.

## 7. Lưu ý rủi ro và xử lý lỗi thường gặp

| Triệu chứng | Kiểm tra trước |
|---|---|
| API dừng ngay khi khởi động | `ConnectionStrings:WpsmDb`, SQL Server/LocalDB đang chạy, database và schema đã tạo |
| 503 từ API | SQL Server không kết nối được hoặc connection string trỏ sai instance/database |
| 401 sau đăng nhập hoặc React quay về trang login | Access token/refresh cookie còn hợp lệ không; API và Vite proxy có chạy đúng port không |
| 403 khi mở route | Role tài khoản không khớp route; không bỏ kiểm tra role để “cho chạy” |
| 409 khi lưu | Unique/check/FK constraint từ SQL; xem log API và nội dung lỗi trả về |
| Dữ liệu UI vẫn là mẫu | Kiểm tra `wpsm-api.js` handler, bootstrap response, `data-wpsm-screen`, query string `?id=` và API proxy |
| Bảng/column mapping lỗi | So sánh SQL script với DbContext/entity; kiểm tra tên PascalCase/casing, precision, nullability và computed field |
| Port bị chiếm | Xem `Properties/launchSettings.json`; dùng port thống nhất hoặc báo nhóm trước khi đổi |

Các file `appsettings*.json` chỉ chứa cấu hình không bí mật có thể commit. Connection string cá nhân, mật khẩu seed và secret khác để trong User Secrets hoặc biến môi trường. Không log password, token, receipt content hoặc thông tin khách hàng nhạy cảm.

## 8. Giới hạn đã biết cần nhớ khi nhận việc

- `Domain` và nhiều thư mục `Application/Features`, `Behaviors` mới là khung; không giả định đã có MediatR, pipeline behavior hoặc đầy đủ use-case handlers như thiết kế mục tiêu.
- `WpsmPortalStore.cs` là store in-memory cũ, không dùng cho luồng mới.
- Các trang đang có là phạm vi Iteration 1; việc một route có giao diện không đồng nghĩa backend đã có toàn bộ nghiệp vụ cuối cùng.
- Email gửi thông tin tài khoản chưa tích hợp; mật khẩu tạm hiện được trả cho Manager một lần để chuyển cho khách.
- NRD receipt hiện lưu file private trên local disk của API; chưa có abstraction cloud/file service.
- Cần tiếp tục đối chiếu Report 4 với kiến trúc React/API và schema hiện tại khi mở rộng các iteration sau.

## 9. Quy ước phối hợp đề xuất

- Mỗi feature có một owner chịu trách nhiệm end-to-end và một người review.
- Một PR tập trung vào một feature; contract hoặc schema thay đổi phải thông báo rõ cho các PR phụ thuộc.
- Thay đổi shared contract/SQL/DbContext/DI do một người điều phối để giảm conflict.
- Không push trực tiếp vào nhánh nhóm nếu quy trình lớp yêu cầu PR.
- Nếu phát hiện thiết kế không khớp tài liệu, ghi nhận và thống nhất hướng trước khi tạo thêm cấu trúc mới.

### Checklist ngắn của thành viên

- [ ] Đọc scope/use case/screen design của feature.
- [ ] Có SQL Server local, chạy schema và seed được.
- [ ] API và Web chạy được bằng port nhóm thống nhất.
- [ ] Biết interface/repository và endpoint feature sẽ dùng.
- [ ] Không gọi DbContext từ Web/API endpoint.
- [ ] Không commit secrets hoặc `bin/obj`.
- [ ] Build và kiểm tra luồng feature trên database trước khi gửi PR.
