import { create } from 'zustand';
import { devtools } from 'zustand/middleware';

interface UIState {
  // Loading states
  isLoading: boolean;
  loadingMessage: string | null;

  // Error states
  globalError: string | null;
  showError: boolean;

  // Success states
  successMessage: string | null;
  showSuccess: boolean;

  // Modal states
  isModalOpen: boolean;
  modalContent: React.ReactNode | null;
  modalTitle: string | null;

  // Navigation
  currentStep: number;
  totalSteps: number;

  // Theme
  isDarkMode: boolean;

  // Actions
  setLoading: (loading: boolean, message?: string) => void;
  setGlobalError: (error: string | null) => void;
  setSuccessMessage: (message: string | null) => void;
  showModal: (title: string, content: React.ReactNode) => void;
  hideModal: () => void;
  setCurrentStep: (step: number) => void;
  setTotalSteps: (steps: number) => void;
  toggleDarkMode: () => void;
  clearMessages: () => void;
  resetUI: () => void;
}

export const useUIStore = create<UIState>()(
  devtools(
    (set) => ({
      // Initial state
      isLoading: false,
      loadingMessage: null,
      globalError: null,
      showError: false,
      successMessage: null,
      showSuccess: false,
      isModalOpen: false,
      modalContent: null,
      modalTitle: null,
      currentStep: 1,
      totalSteps: 3,
      isDarkMode: false,

      // Actions
      setLoading: (loading, message) =>
        set(
          {
            isLoading: loading,
            loadingMessage: message || null,
          },
          false,
          'setLoading'
        ),

      setGlobalError: (error) =>
        set(
          {
            globalError: error,
            showError: !!error,
          },
          false,
          'setGlobalError'
        ),

      setSuccessMessage: (message) =>
        set(
          {
            successMessage: message,
            showSuccess: !!message,
          },
          false,
          'setSuccessMessage'
        ),

      showModal: (title, content) =>
        set(
          {
            isModalOpen: true,
            modalTitle: title,
            modalContent: content,
          },
          false,
          'showModal'
        ),

      hideModal: () =>
        set(
          {
            isModalOpen: false,
            modalContent: null,
            modalTitle: null,
          },
          false,
          'hideModal'
        ),

      setCurrentStep: (step) =>
        set({ currentStep: step }, false, 'setCurrentStep'),

      setTotalSteps: (steps) =>
        set({ totalSteps: steps }, false, 'setTotalSteps'),

      toggleDarkMode: () =>
        set(
          (state) => ({ isDarkMode: !state.isDarkMode }),
          false,
          'toggleDarkMode'
        ),

      clearMessages: () =>
        set(
          {
            globalError: null,
            showError: false,
            successMessage: null,
            showSuccess: false,
          },
          false,
          'clearMessages'
        ),

      resetUI: () =>
        set(
          {
            isLoading: false,
            loadingMessage: null,
            globalError: null,
            showError: false,
            successMessage: null,
            showSuccess: false,
            isModalOpen: false,
            modalContent: null,
            modalTitle: null,
            currentStep: 1,
            totalSteps: 3,
          },
          false,
          'resetUI'
        ),
    }),
    {
      name: 'ui-store',
    }
  )
);
