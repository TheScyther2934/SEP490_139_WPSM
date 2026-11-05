import ReportLayout from "../../../components/reports/ReportLayout";
import HallUtilizationReport from "./components/HallUtilizationReport";

// A-10
export default function HallUtilization() {
  return <ReportLayout code="A-10" title="Công suất sảnh" render={(report) => <HallUtilizationReport report={report} />} />;
}
