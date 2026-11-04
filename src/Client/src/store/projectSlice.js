import { createAsyncThunk, createSlice } from "@reduxjs/toolkit";
import { projectService } from "../services/projectService";
import { errorMessage } from "../utils/format";

// Manager project currently opened under /manager/projects/:projectCode.
// The snapshot carries the project, its roadmap, the catalog and the PriceFrames (M-04, M-05).
export const loadProject = createAsyncThunk("project/load", async (projectCode, { rejectWithValue }) => {
  try {
    const snapshot = await projectService.getSnapshot(projectCode);
    if (!snapshot.project || snapshot.project.id !== projectCode) return rejectWithValue("Không tìm thấy dự án hoặc dự án không thuộc phạm vi của bạn.");
    return snapshot;
  } catch (error) {
    return rejectWithValue(errorMessage(error));
  }
});

const projectSlice = createSlice({
  name: "project",
  initialState: { code: null, status: "idle", error: null, snapshot: null },
  reducers: {
    // Save and freeze answer the updated project; keep the rest of the snapshot.
    projectUpdated: (state, { payload }) => {
      if (state.snapshot) state.snapshot.project = payload;
    },
  },
  extraReducers: (builder) => {
    builder
      .addCase(loadProject.pending, (state, { meta }) => {
        if (state.code !== meta.arg) state.snapshot = null;
        state.code = meta.arg;
        state.status = "loading";
        state.error = null;
      })
      .addCase(loadProject.fulfilled, (state, { payload, meta }) => {
        if (state.code !== meta.arg) return;
        state.status = "ready";
        state.snapshot = payload;
      })
      .addCase(loadProject.rejected, (state, { payload, meta }) => {
        if (state.code !== meta.arg) return;
        state.status = "error";
        state.error = payload ?? "Không tải được dự án.";
      });
  },
});

export const { projectUpdated } = projectSlice.actions;
export const selectProjectState = (state) => state.project;
export default projectSlice.reducer;
