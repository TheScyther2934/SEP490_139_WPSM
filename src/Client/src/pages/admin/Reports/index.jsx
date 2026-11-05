import ReportLayout from "../../../components/reports/ReportLayout";
import { useReportRange } from "../../../components/reports/useReportRange";
import ReportSummary from "./components/ReportSummary";

// A-09
export default function Reports() {
  const { query } = useReportRange();
  return <ReportLayout code="A-09" title="Báo cáo" render={(report) => <ReportSummary report={report} query={query} />} />;
}
