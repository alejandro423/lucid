using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class MenuController : MonoBehaviour
{
    [Header("Logo")]
    public CanvasGroup logo;

    [Header("Fondo de botones")]
    public RectTransform fondoBotones;

    [Header("Contenido de botones")]
    public CanvasGroup contenidoBotones;

    [Header("Duración de animaciones")]
    public float duracionLogo = 0.8f;
    public float duracionFondoBotones = 0.7f;
    public float duracionBotones = 0.5f;

    [Header("Distancia de entrada")]
    public float distanciaEntrada = 900f;

    private Vector2 posicionFinalFondo;
    private Vector2 posicionInicialFondo;


    private void Start()
    {

        posicionFinalFondo = fondoBotones.anchoredPosition;

        posicionInicialFondo =
            posicionFinalFondo + new Vector2(-1300f, 0f);

        fondoBotones.anchoredPosition = posicionInicialFondo;

        logo.alpha = 0f;

        contenidoBotones.alpha = 0f;

        StartCoroutine(AnimacionEntrada());
    }


    private IEnumerator AnimacionEntrada()
    {

        yield return StartCoroutine(
            Fade(
                logo,
                0f,
                1f,
                duracionLogo
            )
        );

        yield return StartCoroutine(
            MoverFondoBotones(
                posicionInicialFondo,
                posicionFinalFondo,
                duracionFondoBotones
            )
        );

        yield return StartCoroutine(
            Fade(
                contenidoBotones,
                0f,
                1f,
                duracionBotones
            )
        );
    }

    private IEnumerator Fade(
        CanvasGroup grupo,
        float inicio,
        float final,
        float duracion)
    {
        float tiempo = 0f;

        while (tiempo < duracion)
        {
            tiempo += Time.deltaTime;

            float progreso = tiempo / duracion;

            grupo.alpha = Mathf.Lerp(
                inicio,
                final,
                progreso
            );

            yield return null;
        }

        grupo.alpha = final;
    }

    private IEnumerator MoverFondoBotones(
        Vector2 inicio,
        Vector2 final,
        float duracion)
    {
        float tiempo = 0f;

        while (tiempo < duracion)
        {
            tiempo += Time.deltaTime;

            float progreso = tiempo / duracion;

            progreso = Mathf.SmoothStep(
                0f,
                1f,
                progreso
            );

            fondoBotones.anchoredPosition =
                Vector2.Lerp(
                    inicio,
                    final,
                    progreso
                );

            yield return null;
        }

        fondoBotones.anchoredPosition = final;
    }

    public void Jugar()
    {
        SceneManager.LoadScene("Juego");
    }

    public void Ajustes()
    {
        SceneManager.LoadScene("Ajustes");
    }

    public void Salir()
    {
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}