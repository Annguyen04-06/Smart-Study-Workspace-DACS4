/**
 * Quản lý đóng/mở hộp thoại Modal tái sử dụng qua thuộc tính data-modal-close và phím Escape.
 */

let isGlobalEscBound = false;

function bindGlobalEscOnce() {
    if (isGlobalEscBound) {
        return;
    }
    isGlobalEscBound = true;
    document.addEventListener('keydown', (event) => {
        if (event.key !== 'Escape') {
            return;
        }
        const openModals = document.querySelectorAll('.ss-modal-backdrop:not(.hidden)');
        const topModal = openModals[openModals.length - 1];
        if (topModal) {
            closeModal(topModal.id);
        }
    });
}

export function openModal(modalId) {
    bindGlobalEscOnce();
    const modalEl = document.getElementById(modalId);
    if (!modalEl) {
        return;
    }
    modalEl.classList.remove('hidden');
    document.body.classList.add('overflow-hidden');

    if (!modalEl.dataset.boundBackdrop) {
        modalEl.dataset.boundBackdrop = 'true';
        modalEl.addEventListener('click', (event) => {
            if (event.target === modalEl || event.target.closest('[data-modal-close]')) {
                closeModal(modalId);
            }
        });
    }
}

export function closeModal(modalId) {
    const modalEl = document.getElementById(modalId);
    if (!modalEl) {
        return;
    }
    modalEl.classList.add('hidden');
    const stillOpen = document.querySelectorAll('.ss-modal-backdrop:not(.hidden)').length;
    if (stillOpen === 0) {
        document.body.classList.remove('overflow-hidden');
    }
}
