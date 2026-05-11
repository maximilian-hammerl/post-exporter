package dev.hammerl.post_exporter

import androidx.compose.ui.window.Window
import androidx.compose.ui.window.application

fun main() = application {
    Window(
        onCloseRequest = ::exitApplication,
        title = "PostExporter",
    ) {
        App()
    }
}