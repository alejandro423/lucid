# Feature: maquetas UI individuales estilo OSB

## Objetivo
Crear 3 pantallas individuales (ajustes, sonidos, controles) como maquetas HTML puras, sin assets externos, inspiradas en open-shapes-and-beats.

## Problema
El equipo pidio pantallas de ajustes/carga/etc. Se decidio hacerlas individuales y modulares, no wizard paso a paso.

## Por que
- Jugador espera acceso directo en cualquier momento (pausa, menu).
- Maquetas feas validan layout antes de Unity UGUI definitivo.
- Estilo referencia: OSB (fondo oscuro, formas geometricas neon, sidebar categorias, sliders/toggles).

## Alcance autorizado
- `mockups/ajustes.html` - categorias + navegacion a sonidos/controles
- `mockups/sonidos.html` - volumen master/musica/sfx, mute toggles, test sonido
- `mockups/controles.html` - lista remapeable WASD/Espacio/Shift/Q/E/Tab + perfil mando
- Solo HTML+CSS inline, cero assets externos, cero libs CDN.
- No tocar `My project (2)/`, no tocar `Assets/`.

## Checklist
- [ ] mockups/ajustes.html existe y abre sin errores
- [ ] mockups/sonidos.html existe con sliders/toggles funcionales en JS inline
- [ ] mockups/controles.html existe con tabla de binds
- [ ] Estilo OSB: fondo #0a0a12, cian/magenta/amarillo, cuadrados girando con CSS, topbar
- [ ] Verificado: abrir los 3 archivos en browser / `python3 -m http.server`

## Criterios de aceptacion
- 3 archivos individuales, navegables entre si con links.
- Sin <img>, sin url() externa, sin @import, todo CSS inline.
- Feo permitido, pero layout claro y textos en espanol.

## Route
- delegated direct (writer trigger: 3 archivos no triviales). Skill: ui-ux-pro-max.
- TDD: off (maquetas estaticas, verificacion manual de apertura).

## Progreso
- 2026-09-28: doc creado, delegando writer.
