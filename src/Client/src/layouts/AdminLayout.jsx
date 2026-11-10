import { ThemeProvider } from "@mui/material/styles";
import DashboardIcon from "@mui/icons-material/Dashboard";
import CategoryIcon from "@mui/icons-material/Category";
import PriceChangeIcon from "@mui/icons-material/PriceChange";
import GavelIcon from "@mui/icons-material/Gavel";
import AssessmentIcon from "@mui/icons-material/Assessment";
import PhotoLibraryIcon from "@mui/icons-material/PhotoLibrary";
import WarningIcon from "@mui/icons-material/Warning";
import ReportProblemIcon from "@mui/icons-material/ReportProblem";
import RuleIcon from "@mui/icons-material/Rule";
import TopNavShell from "./TopNavShell";
import adminTheme, { NAVY } from "../adminTheme";
import { PATHS } from "../routes/paths";

const A = PATHS.admin;
// Order of the A-01 navigation (SRS 10.1): catalog → price frames → policies → reports → monitoring screens.
const ITEMS = [
  { label: "Tổng quan", icon: DashboardIcon, to: A.dashboard },
  { label: "Danh mục", icon: CategoryIcon, to: A.catalog },
  { label: "Khung giá", icon: PriceChangeIcon, to: A.priceFrames },
  { label: "Chính sách", icon: GavelIcon, to: A.policies },
  { label: "Báo cáo", icon: AssessmentIcon, to: A.reports },
  { label: "Ảnh minh chứng", icon: PhotoLibraryIcon, to: A.photoFeed },
  { label: "Cảnh báo quá hạn", icon: WarningIcon, to: A.overdueAlerts },
  { label: "Khiếu nại", icon: ReportProblemIcon, to: A.complaints },
  { label: "Ngoại lệ", icon: RuleIcon, to: A.exceptionRequests },
];

export default function AdminLayout() {
  return (
    <ThemeProvider theme={adminTheme}>
      <TopNavShell brand="WPSM" items={ITEMS} barColor={NAVY} />
    </ThemeProvider>
  );
}
