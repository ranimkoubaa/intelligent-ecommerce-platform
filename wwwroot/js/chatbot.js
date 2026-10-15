/**
 * NexusBot - Floating AI Chatbot for Product & Stock Management
 * Supports Natural Language Inquiries and Visual Image Product Recognition
 */

(function () {
    'use strict';

    $(document).ready(function () {
        const launcher = $('#chatbotLauncher');
        const windowEl = $('#chatbotWindow');
        const closeBtn = $('#chatbotCloseBtn');
        const clearBtn = $('#chatbotClearBtn');
        const form = $('#chatbotForm');
        const input = $('#chatbotInputText');
        const fileInput = $('#chatbotFileInput');
        const attachBtn = $('#chatbotAttachBtn');
        const previewBar = $('#chatbotPreviewBar');
        const imgPreview = $('#chatbotImgPreview');
        const previewName = $('#chatbotPreviewName');
        const removeImgBtn = $('#chatbotRemoveImgBtn');
        const messagesContainer = $('#chatbotMessages');
        const typingEl = $('#chatbotTyping');
        const dropOverlay = $('#chatbotDropOverlay');
        const unreadBadge = $('#chatbotUnreadBadge');

        let isInitialized = false;
        let selectedFile = null;

        // Toggle Open / Close
        launcher.on('click', function () {
            if (windowEl.hasClass('d-none')) {
                windowEl.removeClass('d-none');
                launcher.addClass('active');
                unreadBadge.addClass('d-none');
                if (!isInitialized) {
                    showWelcomeMessage();
                    isInitialized = true;
                }
                setTimeout(() => input.focus(), 200);
            } else {
                windowEl.addClass('d-none');
                launcher.removeClass('active');
            }
        });

        closeBtn.on('click', function () {
            windowEl.addClass('d-none');
            launcher.removeClass('active');
        });

        clearBtn.on('click', function () {
            messagesContainer.empty();
            clearSelectedImage();
            showWelcomeMessage();
        });

        // Trigger file input
        attachBtn.on('click', function () {
            fileInput.click();
        });

        fileInput.on('change', function (e) {
            const file = e.target.files[0];
            if (file) {
                handleFileSelected(file);
            }
        });

        removeImgBtn.on('click', function () {
            clearSelectedImage();
        });

        // Drag & Drop support
        const dropZone = windowEl[0];

        ['dragenter', 'dragover'].forEach(eventName => {
            dropZone.addEventListener(eventName, function (e) {
                e.preventDefault();
                e.stopPropagation();
                dropOverlay.removeClass('d-none');
            }, false);
        });

        ['dragleave', 'drop'].forEach(eventName => {
            dropZone.addEventListener(eventName, function (e) {
                e.preventDefault();
                e.stopPropagation();
                dropOverlay.addClass('d-none');
            }, false);
        });

        dropZone.addEventListener('drop', function (e) {
            const dt = e.dataTransfer;
            const files = dt.files;
            if (files && files.length > 0 && files[0].type.startsWith('image/')) {
                handleFileSelected(files[0]);
            }
        }, false);

        // Clipboard paste image support
        window.addEventListener('paste', function (e) {
            if (!windowEl.hasClass('d-none')) {
                const items = (e.clipboardData || e.originalEvent.clipboardData).items;
                for (let index in items) {
                    const item = items[index];
                    if (item.kind === 'file' && item.type.startsWith('image/')) {
                        const blob = item.getAsFile();
                        handleFileSelected(blob);
                        break;
                    }
                }
            }
        });

        function handleFileSelected(file) {
            selectedFile = file;
            const reader = new FileReader();
            reader.onload = function (e) {
                imgPreview.attr('src', e.target.result);
                previewName.text(file.name || 'Image collée');
                previewBar.removeClass('d-none');
                input.attr('placeholder', "Ajoutez une question sur cette photo (ou appuyez sur Entrée)...");
                input.focus();
            };
            reader.readAsDataURL(file);
        }

        function clearSelectedImage() {
            selectedFile = null;
            fileInput.val('');
            previewBar.addClass('d-none');
            imgPreview.attr('src', '');
            input.attr('placeholder', "Demandez le stock d'un produit, un prix ou envoyez une photo...");
        }

        // Form Submit / Send
        form.on('submit', function (e) {
            e.preventDefault();
            const text = input.val().trim();

            if (!text && !selectedFile) {
                return;
            }

            const currentImg = selectedFile ? imgPreview.attr('src') : null;
            const currentFile = selectedFile;

            // Append user message
            appendUserBubble(text, currentImg);

            // Prepare Form Data
            const formData = new FormData();
            if (text) formData.append('message', text);
            if (currentFile) formData.append('image', currentFile);

            // Clear inputs
            input.val('');
            clearSelectedImage();

            // Show typing indicator
            typingEl.removeClass('d-none');
            scrollToBottom();

            // Send to Backend
            $.ajax({
                url: '/Chatbot/SendMessage',
                type: 'POST',
                data: formData,
                processData: false,
                contentType: false,
                success: function (res) {
                    typingEl.addClass('d-none');
                    if (res && res.success && res.data) {
                        appendBotBubble(res.data);
                    } else {
                        appendBotBubble({
                            reply: "Une erreur est survenue lors de l'analyse de votre demande.",
                            products: [],
                            quickReplies: ["📦 Voir les produits en stock", "Recommencer"]
                        });
                    }
                },
                error: function (xhr, status, error) {
                    typingEl.addClass('d-none');
                    console.error("Chatbot request error:", status, error, xhr);
                    var detail = xhr.status ? " (Code HTTP " + xhr.status + ")" : " (Serveur déconnecté)";
                    appendBotBubble({
                        reply: "Désolé, impossible de joindre le serveur" + detail + ". Veuillez vérifier que le serveur est bien démarré.",
                        products: [],
                        quickReplies: ["Réessayer"]
                    });
                }
            });
        });

        // Initial Welcome Message
        function showWelcomeMessage() {
            const welcome = {
                reply: "👋 Bonjour ! Je suis **NexusBot**, votre assistant intelligent.\n\n" +
                       "Posez-moi une question sur nos **produits et stocks**, ou **envoyez-moi une photo** 📷 pour que je l'identifie immédiatement !",
                products: [],
                quickReplies: [
                    "📦 Disponibilité du stock",
                    "💻 Ordinateurs portables",
                    "💰 Moins de 500 DT",
                    "⚠️ Articles en rupture ou stock faible"
                ],
                timestamp: formatCurrentTime()
            };
            appendBotBubble(welcome);
        }

        // Append User Bubble
        function appendUserBubble(text, imgSrc) {
            let html = '<div class="chat-msg user-msg">';
            html += '<div class="msg-bubble user-bubble shadow-sm">';
            if (imgSrc) {
                html += '<img src="' + imgSrc + '" alt="Photo envoyée" class="user-msg-image rounded-3 mb-2" />';
            }
            if (text) {
                html += '<div class="user-msg-text">' + escapeHtml(text) + '</div>';
            }
            html += '<small class="msg-time text-white-50 mt-1 d-block text-end">' + formatCurrentTime() + '</small>';
            html += '</div></div>';

            messagesContainer.append(html);
            scrollToBottom();
        }

        // Append Bot Bubble
        function appendBotBubble(data) {
            let html = '<div class="chat-msg bot-msg">';
            html += '<div class="bot-avatar"><i class="bi bi-robot"></i></div>';
            html += '<div class="bot-msg-content">';
            html += '<div class="msg-bubble bot-bubble shadow-sm">';
            html += '<div class="bot-text">' + formatBotMarkdown(data.reply) + '</div>';

            // Render Product Cards
            if (data.products && data.products.length > 0) {
                html += '<div class="chatbot-products-carousel mt-2">';
                data.products.forEach(function (prod) {
                    html += '<div class="chatbot-product-card card shadow-sm">';
                    html += '<div class="d-flex gap-2">';
                    html += '<img src="' + prod.imageUrl + '" alt="' + escapeHtml(prod.name) + '" class="chatbot-product-thumb" />';
                    html += '<div class="flex-grow-1 min-w-0">';
                    html += '<span class="badge bg-secondary-subtle text-secondary small text-xs">' + escapeHtml(prod.categoryName) + '</span>';
                    html += '<h6 class="chatbot-product-title text-truncate mb-1" title="' + escapeHtml(prod.name) + '">' + escapeHtml(prod.name) + '</h6>';
                    html += '<div class="d-flex align-items-center justify-content-between">';
                    html += '<span class="chatbot-product-price">' + prod.price.toFixed(2) + ' DT</span>';
                    html += '<span class="badge bg-' + prod.stockBadgeClass + '-subtle text-' + prod.stockBadgeClass + ' border border-' + prod.stockBadgeClass + '-subtle rounded-pill text-xs">' + prod.stockStatus + '</span>';
                    html += '</div>';
                    html += '</div></div>';

                    if (prod.confidencePercent) {
                        html += '<div class="confidence-indicator mt-1">';
                        html += '<i class="bi bi-bullseye text-primary me-1"></i>';
                        html += '<span>Correspondance photo : <strong>' + prod.confidencePercent + '%</strong></span>';
                        html += '</div>';
                    }

                    html += '<div class="d-flex gap-1 mt-2 pt-2 border-top">';
                    html += '<button type="button" class="btn btn-sm btn-primary flex-grow-1 chatbot-card-add-btn" data-id="' + prod.productId + '">';
                    html += '<i class="bi bi-cart-plus me-1"></i> Ajouter';
                    html += '</button>';
                    html += '<a href="/Product/Details/' + prod.productId + '" class="btn btn-sm btn-outline-secondary" title="Fiche produit">';
                    html += '<i class="bi bi-eye"></i>';
                    html += '</a>';
                    html += '</div>';

                    html += '</div>';
                });
                html += '</div>';
            }

            html += '<small class="msg-time text-muted mt-1 d-block text-end">' + (data.timestamp || formatCurrentTime()) + '</small>';
            html += '</div>';

            // Render Quick Replies
            if (data.quickReplies && data.quickReplies.length > 0) {
                html += '<div class="chatbot-quick-chips d-flex flex-wrap gap-1 mt-2">';
                data.quickReplies.forEach(function (qr) {
                    html += '<button type="button" class="chatbot-chip-btn" data-query="' + escapeHtml(qr) + '">';
                    html += escapeHtml(qr);
                    html += '</button>';
                });
                html += '</div>';
            }

            html += '</div></div>';

            messagesContainer.append(html);
            scrollToBottom();
        }

        // Quick chip click in widget
        $(document).on('click', '.chatbot-chip-btn', function () {
            const query = $(this).data('query');
            input.val(query);
            form.submit();
        });

        // Add to cart from widget product card
        $(document).on('click', '.chatbot-card-add-btn', function () {
            const btn = $(this);
            const productId = btn.data('id');
            btn.prop('disabled', true).html('<span class="spinner-border spinner-border-sm me-1"></span>');

            $.ajax({
                url: '/Chatbot/AddToCartAjax',
                type: 'POST',
                data: { productId: productId },
                success: function (res) {
                    if (res.success) {
                        btn.removeClass('btn-primary').addClass('btn-success').html('<i class="bi bi-check2"></i> Ajouté');
                        
                        // Update cart badge in navbar
                        const navCartBadge = $('a[href*="/Panier"] .badge');
                        if (navCartBadge.length) {
                            navCartBadge.text(res.cartCount);
                        } else {
                            $('a[href*="/Panier"]').append('<span class="badge bg-danger rounded-pill px-2 py-1 ms-1">' + res.cartCount + '</span>');
                        }

                        setTimeout(function () {
                            btn.removeClass('btn-success').addClass('btn-primary').prop('disabled', false).html('<i class="bi bi-cart-plus me-1"></i> Ajouter');
                        }, 2500);
                    } else {
                        alert(res.message || "Impossible d'ajouter ce produit au panier.");
                        btn.prop('disabled', false).html('<i class="bi bi-cart-plus me-1"></i> Ajouter');
                    }
                },
                error: function () {
                    btn.prop('disabled', false).html('<i class="bi bi-cart-plus me-1"></i> Ajouter');
                }
            });
        });

        function scrollToBottom() {
            if (messagesContainer[0]) {
                messagesContainer.scrollTop(messagesContainer[0].scrollHeight);
            }
        }

        function formatCurrentTime() {
            const now = new Date();
            return now.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
        }

        function formatBotMarkdown(text) {
            if (!text) return "";
            let res = escapeHtml(text);
            res = res.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>');
            res = res.replace(/\*(.*?)\*/g, '<em>$1</em>');
            res = res.replace(/\n\n/g, '<br/><br/>');
            res = res.replace(/\n/g, '<br/>');
            return res;
        }

        function escapeHtml(string) {
            const map = {
                '&': '&amp;',
                '<': '&lt;',
                '>': '&gt;',
                '"': '&quot;',
                "'": '&#39;'
            };
            return String(string).replace(/[&<>"']/g, function (s) {
                return map[s];
            });
        }
    });
})();
