import ReportLayout from "../../../components/reports/ReportLayout";
import CrStatisticsReport from "./components/CrStatisticsReport";

// A-12
export default function CrStatistics() {
  return <ReportLayout code="A-12" title="Thống kê yêu cầu thay đổi" render={(report) => <CrStatisticsReport report={report} />} />;
}
