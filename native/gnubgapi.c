/*
 * GNU Backgammon API (initial stub)
 *
 * NOTE: This file is a scaffold. It provides a stable C ABI and error
 * handling hooks for a future P/Invoke layer, but it does not yet wire
 * into gnubg evaluation internals.
 */
#include "gnubgapi.h"

#include "eval.h"
#include "matchequity.h"
#include "matchid.h"
#include "positionid.h"
#include "lib/gnubg-types.h"
#include "multithread.h"
#include "dice.h"
/* We include only the declarations we need from analysis.h/backgammon.h
 * to avoid pulling in the full gnubg application dependencies. */
static void init_standard_board(TanBoard anBoard) {
    PositionFromID(anBoard, "4HPwATDgc/ABMA");
}

#include <glib/gstdio.h>
#include <limits.h>
#include <stdlib.h>
#include <string.h>

/*
 * Stubs for symbols referenced by gnubg internals (dice.c, mtsupport.c)
 * that are normally provided by the full gnubg application. The API library
 * does not need these features.
 */
void SetRNG(rng *prng, rngcontext *rngctx, rng rngNew, char *szSeed) {
    (void)prng; (void)rngctx; (void)rngNew; (void)szSeed;
}

int GetManualDice(unsigned int anDice[2]) {
    (void)anDice;
    return -1;
}

/* MT_CloseThreads is provided by multithread.c — no stub needed */

/*
 * Additional stubs for symbols required by rollout.c and multithread.c.
 * These are normally provided by the full gnubg application (UI, autosave,
 * game state tracking). The API library runs rollouts without a UI.
 */

#include "rollout.h"
#include "backgammon.h"
#include "format.h"
#include "movefilters.inc"

/* Global state variables referenced by rollout.c */
int fAutoCrawford = FALSE;
int fAutoSaveRollout = FALSE;
int fShowProgress = FALSE;
int fOutputMWC = FALSE;
int fAnalysisRunning = FALSE;
int nAutoSaveTime = 0;
matchstate ms;
rngcontext *rngctxRollout = NULL;
rolloutcontext rcRollout = {
    {
     { .fCubeful = TRUE, .nPlies = 2, .fUsePrune = TRUE, .fDeterministic = TRUE, .rNoise = 0.0f },
     { .fCubeful = TRUE, .nPlies = 2, .fUsePrune = TRUE, .fDeterministic = TRUE, .rNoise = 0.0f }
    },
    {
     { .fCubeful = TRUE, .nPlies = 0, .fUsePrune = TRUE, .fDeterministic = TRUE, .rNoise = 0.0f },
     { .fCubeful = TRUE, .nPlies = 0, .fUsePrune = TRUE, .fDeterministic = TRUE, .rNoise = 0.0f }
    },
    {
     { .fCubeful = TRUE, .nPlies = 2, .fUsePrune = TRUE, .fDeterministic = TRUE, .rNoise = 0.0f },
     { .fCubeful = TRUE, .nPlies = 2, .fUsePrune = TRUE, .fDeterministic = TRUE, .rNoise = 0.0f }
    },
    {
     { .fCubeful = TRUE, .nPlies = 0, .fUsePrune = TRUE, .fDeterministic = TRUE, .rNoise = 0.0f },
     { .fCubeful = TRUE, .nPlies = 0, .fUsePrune = TRUE, .fDeterministic = TRUE, .rNoise = 0.0f }
    },
    { .fCubeful = TRUE, .nPlies = 2, .fUsePrune = TRUE, .fDeterministic = TRUE, .rNoise = 0.0f },
    { .fCubeful = TRUE, .nPlies = 2, .fUsePrune = TRUE, .fDeterministic = TRUE, .rNoise = 0.0f },
    { MOVEFILTER_NORMAL, MOVEFILTER_NORMAL },
    { MOVEFILTER_NORMAL, MOVEFILTER_NORMAL },
    TRUE,       /* cubeful */
    TRUE,       /* variance reduction */
    FALSE,      /* initial */
    TRUE,       /* rotate */
    TRUE,       /* truncBearoff2 */
    TRUE,       /* truncBearoffOS */
    FALSE,      /* lateEvals */
    FALSE,      /* doTruncate */
    FALSE,      /* stopOnSTD */
    FALSE,      /* stopOnJsd */
    FALSE,      /* stopMoveOnJsd */
    10,         /* nTruncate */
    1296,       /* nTrials */
    5,          /* nLate */
    RNG_MERSENNE,
    0,          /* seed */
    324,        /* minimumGames */
    0.01f,      /* rStdLimit */
    324,        /* minimumJsdGames */
    2.33f,      /* rJsdLimit */
    0, 0.0, 0   /* nGamesDone, rStoppedOnJSD, nSkip */
};

/* msBoard() is a function in gnubg — returns the board from the global matchstate */
ConstTanBoard msBoard(void) {
    return (ConstTanBoard) ms.anBoard;
}

/* UI / game-state stubs */
void ProcessEvents(void) { }
void ChangeGame(listOLD *plGameNew) { (void)plGameNew; }
moverecord *get_current_moverecord(int *pfHistory) { (void)pfHistory; return NULL; }
double get_time(void) { return 0.0; }
gboolean save_autosave(gpointer unused) { (void)unused; return FALSE; }

/* Stubs for symbols referenced by format.c and dice.c */
const char *aszSkillType[] = { "Very bad", "Bad", "Doubtful", NULL };
player ap[2] = { {0}, {0} };
#include "export.h"
exportsetup exsExport = { 0 };

struct gnubgapi_context {
    int initialized;
};

static char g_last_error[256] = "not initialized";

static void set_last_error(const char *msg) {
    if (!msg) {
        g_last_error[0] = '\0';
        return;
    }
    strncpy(g_last_error, msg, sizeof(g_last_error) - 1);
    g_last_error[sizeof(g_last_error) - 1] = '\0';
}

void gnubgapi_get_version(uint32_t *major, uint32_t *minor, uint32_t *patch) {
    if (major) { *major = GNUBGAPI_VERSION_MAJOR; }
    if (minor) { *minor = GNUBGAPI_VERSION_MINOR; }
    if (patch) { *patch = GNUBGAPI_VERSION_PATCH; }
}

const char *gnubgapi_get_last_error(void) {
    return g_last_error;
}

gnubgapi_context *gnubgapi_create(void) {
    gnubgapi_context *ctx = (gnubgapi_context *)malloc(sizeof(gnubgapi_context));
    if (!ctx) {
        set_last_error("out of memory");
        return NULL;
    }
    ctx->initialized = 0;
    set_last_error("not initialized");
    return ctx;
}

void gnubgapi_destroy(gnubgapi_context *ctx) {
    if (!ctx) {
        return;
    }
    gnubgapi_shutdown(ctx);
    free(ctx);
}

gnubgapi_status gnubgapi_init(
    gnubgapi_context *ctx,
    const char *weights_path,
    const char *weights_binary_path,
    const char *data_dir,
    int no_bearoff
) {
    if (!ctx) {
        set_last_error("context is null");
        return GNUBGAPI_E_INVALID_ARGUMENT;
    }
    if (!weights_path || !weights_binary_path) {
        set_last_error("weights_path or weights_binary_path is null");
        return GNUBGAPI_E_INVALID_ARGUMENT;
    }
    if (data_dir && data_dir[0]) {
        /*
         * Override the package data directory so EvalInitialise can find
         * bearoff databases (gnubg_ts0.bd, gnubg_os0.bd) and InitMatchEquity
         * can find the MET (met/Kazaross-XG2.xml) in data_dir.
         *
         * Resolve data_dir to an absolute path FIRST, then publish it to
         * pkg_datadir. We do NOT chdir — chdir + relative pkg_datadir was
         * a footgun: callers passing a relative data_dir like "gnubg-data"
         * would chdir into it, then path resolution against the same
         * relative pkg_datadir would double-prefix and miss the file.
         * Bearoff/MET would then silently fall through to gnubg's built-in
         * default MET, which is what produced NaN cubeful equities for
         * match-play evaluations even on opening positions.
         */
        extern char *pkg_datadir;
        g_free(pkg_datadir);
        if (g_path_is_absolute(data_dir)) {
            pkg_datadir = g_strdup(data_dir);
        } else {
            char *cwd = g_get_current_dir();
            pkg_datadir = g_build_filename(cwd, data_dir, NULL);
            g_free(cwd);
        }
        if (!g_file_test(pkg_datadir, G_FILE_TEST_IS_DIR)) {
            set_last_error("data_dir does not exist or is not a directory");
            return GNUBGAPI_E_INVALID_ARGUMENT;
        }
    }
    MT_InitThreads();
    MT_StartThreads();
    EvalInitialise((char *)weights_path, (char *)weights_binary_path, no_bearoff, NULL);

    /*
     * Load the match equity table. gnubg's main app does this in gnubg.c
     * via InitMatchEquity(BuildFilename2("met", "Kazaross-XG2.xml")). We
     * MUST do the same — without it, every match-play cubeful evaluation
     * is garbage because mwc2eq reads from an uninitialised MET, returning
     * NaN / 1.0 / -1.0 instead of real match-equity-converted values.
     * This was historically missing from the wrapper, which is why all
     * match-play move evaluations came back as -1 or 0 across the board.
     */
    {
        extern char *pkg_datadir;
        char *met_path = g_build_filename(pkg_datadir, "met", "Kazaross-XG2.xml", NULL);
        InitMatchEquity(met_path);  /* returns void; on failure it falls back to a built-in MET */
        g_free(met_path);
    }

    /* Initialise the RNG context used by rollouts (same as gnubg.c main) */
    if (!rngctxRollout) {
        rngctxRollout = InitRNG(&rcRollout.nSeed, NULL, TRUE, rcRollout.rngRollout);
    }

    ctx->initialized = 1;
    set_last_error("");
    return GNUBGAPI_OK;
}

void gnubgapi_shutdown(gnubgapi_context *ctx) {
    if (!ctx) {
        return;
    }
    if (ctx->initialized) {
        EvalShutdown();
        MT_Close();
        ctx->initialized = 0;
    }
}

/*
 * Helper: validate context/position, parse board and cubeinfo from IDs.
 * Shared by evaluate_position and evaluate_position_full.
 */
static gnubgapi_status parse_position_and_cubeinfo(
    gnubgapi_context *ctx,
    const char *position_id,
    const char *match_id,
    TanBoard board,
    cubeinfo *ci
) {
    if (!ctx) {
        set_last_error("context is null");
        return GNUBGAPI_E_INVALID_ARGUMENT;
    }
    if (!position_id || !position_id[0]) {
        set_last_error("position_id is null or empty");
        return GNUBGAPI_E_INVALID_ARGUMENT;
    }
    if (!ctx->initialized) {
        set_last_error("context not initialized");
        return GNUBGAPI_E_NOT_INITIALIZED;
    }

    if (!PositionFromID(board, position_id)) {
        set_last_error("invalid position_id");
        return GNUBGAPI_E_INVALID_ARGUMENT;
    }

    /* match_id is required. gnubg's own model has no concept of "missing
     * match context" — money games are encoded as match_id with nMatchTo=0
     * (cube/owner/Jacoby still meaningful). Callers must always supply a
     * valid 12-char base64 match_id; ambiguity here would mask real bugs. */
    if (!match_id || !match_id[0]) {
        set_last_error("match_id is required");
        return GNUBGAPI_E_INVALID_ARGUMENT;
    }

    unsigned int anDice[2];
    int fTurn = 0;
    int fResigned = 0;
    int fDoubled = 0;
    int fMove = 0;
    int fCubeOwner = -1;
    int fCrawford = 0;
    int nMatchTo = 0;
    int anScore[2] = {0, 0};
    int nCube = 1;
    int fJacoby = 0;
    gamestate gs;

    if (MatchFromID(anDice, &fTurn, &fResigned, &fDoubled, &fMove, &fCubeOwner,
                    &fCrawford, &nMatchTo, anScore, &nCube, &fJacoby, &gs, match_id) != 0) {
        set_last_error("invalid match_id");
        return GNUBGAPI_E_INVALID_ARGUMENT;
    }

    if (nMatchTo > 0) {
        if (SetCubeInfo(ci, nCube, fCubeOwner, fMove, nMatchTo, anScore, fCrawford, fJacoby, 0,
                        VARIATION_STANDARD) != 0) {
            set_last_error("failed to set cube info (match)");
            return GNUBGAPI_E_INTERNAL;
        }
    } else {
        if (SetCubeInfoMoney(ci, nCube, fCubeOwner, fMove, fJacoby, 0, VARIATION_STANDARD) != 0) {
            set_last_error("failed to set cube info (money)");
            return GNUBGAPI_E_INTERNAL;
        }
    }

    return GNUBGAPI_OK;
}

gnubgapi_status gnubgapi_evaluate_position(
    gnubgapi_context *ctx,
    const char *position_id,
    const char *match_id,
    double *out_equity,
    double *out_cubeful_equity
) {
    if (!out_equity) {
        set_last_error("out_equity is null");
        return GNUBGAPI_E_INVALID_ARGUMENT;
    }

    TanBoard board;
    cubeinfo ci;
    float ar[NUM_ROLLOUT_OUTPUTS];

    gnubgapi_status st = parse_position_and_cubeinfo(ctx, position_id, match_id, board, &ci);
    if (st != GNUBGAPI_OK) return st;

    if (GeneralEvaluationE(ar, (ConstTanBoard) board, &ci, &ecBasic) != 0) {
        set_last_error("evaluation failed");
        return GNUBGAPI_E_INTERNAL;
    }

    *out_equity = (double)ar[OUTPUT_EQUITY];

    if (out_cubeful_equity) {
        evalcontext ec = ecBasic;
        ec.fCubeful = 1;
        if (GeneralEvaluationE(ar, (ConstTanBoard) board, &ci, &ec) != 0) {
            set_last_error("cubeful evaluation failed");
            return GNUBGAPI_E_INTERNAL;
        }
        *out_cubeful_equity = (double)ar[OUTPUT_CUBEFUL_EQUITY];
    }

    set_last_error("");
    return GNUBGAPI_OK;
}

gnubgapi_status gnubgapi_evaluate_position_plied(
    gnubgapi_context *ctx,
    const char *position_id,
    const char *match_id,
    uint32_t n_plies,
    double *out_equity,
    double *out_cubeful_equity
) {
    if (!out_equity) {
        set_last_error("out_equity is null");
        return GNUBGAPI_E_INVALID_ARGUMENT;
    }

    TanBoard board;
    cubeinfo ci;
    float ar[NUM_ROLLOUT_OUTPUTS];

    gnubgapi_status st = parse_position_and_cubeinfo(ctx, position_id, match_id, board, &ci);
    if (st != GNUBGAPI_OK) return st;

    evalcontext ec = {
        .fCubeful = FALSE,
        .nPlies = (unsigned int)n_plies,
        .fUsePrune = (n_plies >= 2) ? TRUE : FALSE,
        .fDeterministic = TRUE,
        .rNoise = 0.0f
    };

    if (GeneralEvaluationE(ar, (ConstTanBoard) board, &ci, &ec) != 0) {
        set_last_error("plied evaluation failed");
        return GNUBGAPI_E_INTERNAL;
    }

    *out_equity = (double)ar[OUTPUT_EQUITY];

    if (out_cubeful_equity) {
        ec.fCubeful = 1;
        if (GeneralEvaluationE(ar, (ConstTanBoard) board, &ci, &ec) != 0) {
            set_last_error("cubeful plied evaluation failed");
            return GNUBGAPI_E_INTERNAL;
        }
        *out_cubeful_equity = (double)ar[OUTPUT_CUBEFUL_EQUITY];
    }

    set_last_error("");
    return GNUBGAPI_OK;
}

gnubgapi_status gnubgapi_evaluate_position_full(
    gnubgapi_context *ctx,
    const char *position_id,
    const char *match_id,
    double out_output[GNUBGAPI_NUM_ROLLOUT_OUTPUTS]
) {
    if (!out_output) {
        set_last_error("out_output is null");
        return GNUBGAPI_E_INVALID_ARGUMENT;
    }

    TanBoard board;
    cubeinfo ci;
    float ar[NUM_ROLLOUT_OUTPUTS];

    gnubgapi_status st = parse_position_and_cubeinfo(ctx, position_id, match_id, board, &ci);
    if (st != GNUBGAPI_OK) return st;

    evalcontext ec = ecBasic;
    ec.fCubeful = 1;

    if (GeneralEvaluationE(ar, (ConstTanBoard) board, &ci, &ec) != 0) {
        set_last_error("evaluation failed");
        return GNUBGAPI_E_INTERNAL;
    }

    for (int i = 0; i < GNUBGAPI_NUM_ROLLOUT_OUTPUTS; i++) {
        out_output[i] = (double)ar[i];
    }

    set_last_error("");
    return GNUBGAPI_OK;
}

gnubgapi_status gnubgapi_evaluate_position_full_plied(
    gnubgapi_context *ctx,
    const char *position_id,
    const char *match_id,
    uint32_t n_plies,
    double out_output[GNUBGAPI_NUM_ROLLOUT_OUTPUTS]
) {
    if (!out_output) {
        set_last_error("out_output is null");
        return GNUBGAPI_E_INVALID_ARGUMENT;
    }

    TanBoard board;
    cubeinfo ci;
    float ar[NUM_ROLLOUT_OUTPUTS];

    gnubgapi_status st = parse_position_and_cubeinfo(ctx, position_id, match_id, board, &ci);
    if (st != GNUBGAPI_OK) return st;

    evalcontext ec = {
        .fCubeful = TRUE,
        .nPlies = (unsigned int)n_plies,
        .fUsePrune = (n_plies >= 2) ? TRUE : FALSE,
        .fDeterministic = TRUE,
        .rNoise = 0.0f
    };

    if (GeneralEvaluationE(ar, (ConstTanBoard) board, &ci, &ec) != 0) {
        set_last_error("full plied evaluation failed");
        return GNUBGAPI_E_INTERNAL;
    }

    for (int i = 0; i < GNUBGAPI_NUM_ROLLOUT_OUTPUTS; i++) {
        out_output[i] = (double)ar[i];
    }

    set_last_error("");
    return GNUBGAPI_OK;
}

/* ------------------------------------------------------------------ */
/* Cube decision API                                                  */
/* ------------------------------------------------------------------ */

gnubgapi_status gnubgapi_evaluate_cube_decision(
    gnubgapi_context *ctx,
    const char *position_id,
    const char *match_id,
    uint32_t n_plies,
    gnubgapi_cube_decision_result *out
) {
    if (!out) {
        set_last_error("out is null");
        return GNUBGAPI_E_INVALID_ARGUMENT;
    }

    TanBoard board;
    cubeinfo ci;

    gnubgapi_status st = parse_position_and_cubeinfo(ctx, position_id, match_id, board, &ci);
    if (st != GNUBGAPI_OK) return st;

    evalcontext ec = {
        .fCubeful = TRUE,
        .nPlies = (unsigned int)n_plies,
        .fUsePrune = (n_plies >= 2) ? TRUE : FALSE,
        .fDeterministic = TRUE,
        .rNoise = 0.0f
    };

    /* GeneralCubeDecisionE writes the 2x7 cubeful outputs in float.
     * Format.c calls it with a NULL evalsetup when doing a plain
     * cubeful eval (no rollout), so we do the same. */
    float aarOutput[2][NUM_ROLLOUT_OUTPUTS];
    if (GeneralCubeDecisionE(aarOutput, (ConstTanBoard) board, &ci, &ec, 0) < 0) {
        set_last_error("cube decision evaluation failed");
        return GNUBGAPI_E_INTERNAL;
    }

    /* FindCubeDecision fills arDouble[4] = {OPTIMAL, NODOUBLE, TAKE, DROP}
     * from the offerer's POV, normalized to money-equity space in match
     * play, and returns the cubedecision enum value (DOUBLE_TAKE,
     * NODOUBLE_TAKE, TOOGOOD_PASS, etc.). */
    float arDouble[NUM_CUBEFUL_OUTPUTS];
    cubedecision cd = FindCubeDecision(arDouble, aarOutput, &ci);

    for (int r = 0; r < 2; r++) {
        for (int c = 0; c < NUM_ROLLOUT_OUTPUTS; c++) {
            out->cubeful_outputs[r][c] = (double) aarOutput[r][c];
        }
    }
    for (int i = 0; i < NUM_CUBEFUL_OUTPUTS; i++) {
        out->equities[i] = (double) arDouble[i];
    }
    out->decision = (int32_t) cd;

    set_last_error("");
    return GNUBGAPI_OK;
}

/* ------------------------------------------------------------------ */
/* Rollout API                                                        */
/* ------------------------------------------------------------------ */

void gnubgapi_rollout_settings_default(gnubgapi_rollout_settings *settings) {
    if (!settings) return;
    settings->n_trials = 1296;
    settings->cubeful = 1;
    settings->variance_reduction = 1;
    settings->chequer_plies = 0;
    settings->cube_plies = 2;
    settings->seed = 0;
    settings->truncate = 1;
    settings->truncate_plies = 10;
}

/*
 * Helper: build a rolloutcontext from the user-facing settings struct.
 * Starts from the global rcRollout defaults and overrides key fields.
 */
static void build_rollout_context(rolloutcontext *rc,
                                  const gnubgapi_rollout_settings *s) {
    *rc = rcRollout;   /* copy defaults */

    rc->nTrials  = s->n_trials;
    rc->fCubeful = s->cubeful ? TRUE : FALSE;
    rc->fVarRedn = s->variance_reduction ? TRUE : FALSE;
    rc->nSeed    = s->seed;

    rc->fDoTruncate    = s->truncate ? TRUE : FALSE;
    rc->nTruncate      = (unsigned short)s->truncate_plies;
    rc->fTruncBearoff2 = TRUE;
    rc->fTruncBearoffOS = TRUE;

    /* Set chequer and cube evaluation plies for both players */
    for (int i = 0; i < 2; i++) {
        rc->aecChequer[i].nPlies       = s->chequer_plies;
        rc->aecChequer[i].fCubeful     = s->cubeful ? TRUE : FALSE;
        rc->aecChequer[i].fUsePrune    = TRUE;
        rc->aecChequer[i].fDeterministic = TRUE;
        rc->aecChequer[i].rNoise       = 0.0f;

        rc->aecCube[i].nPlies          = s->cube_plies;
        rc->aecCube[i].fCubeful        = TRUE;
        rc->aecCube[i].fUsePrune       = TRUE;
        rc->aecCube[i].fDeterministic  = TRUE;
        rc->aecCube[i].rNoise          = 0.0f;
    }

    /* Quasi-random dice (rotate), no late evals, no JSD stopping */
    rc->fRotate       = TRUE;
    rc->fInitial      = FALSE;
    rc->fLateEvals    = FALSE;
    rc->fStopOnSTD    = FALSE;
    rc->fStopOnJsd    = FALSE;
    rc->fStopMoveOnJsd = FALSE;
    rc->rngRollout    = RNG_MERSENNE;
    rc->nGamesDone    = 0;
    rc->rStoppedOnJSD = 0.0f;
    rc->nSkip         = 0;
}

gnubgapi_status gnubgapi_rollout_position(
    gnubgapi_context *ctx,
    const char *position_id,
    const char *match_id,
    const gnubgapi_rollout_settings *settings,
    double out_output[GNUBGAPI_NUM_ROLLOUT_OUTPUTS],
    double out_std_dev[GNUBGAPI_NUM_ROLLOUT_OUTPUTS]
) {
    if (!ctx) {
        set_last_error("context is null");
        return GNUBGAPI_E_INVALID_ARGUMENT;
    }
    if (!position_id || !position_id[0]) {
        set_last_error("position_id is null or empty");
        return GNUBGAPI_E_INVALID_ARGUMENT;
    }
    if (!out_output || !out_std_dev) {
        set_last_error("output arrays are null");
        return GNUBGAPI_E_INVALID_ARGUMENT;
    }
    if (!ctx->initialized) {
        set_last_error("context not initialized");
        return GNUBGAPI_E_NOT_INITIALIZED;
    }

    /* Use caller settings or defaults */
    gnubgapi_rollout_settings defaults;
    if (!settings) {
        gnubgapi_rollout_settings_default(&defaults);
        settings = &defaults;
    }

    TanBoard board;
    cubeinfo ci;

    if (!PositionFromID(board, position_id)) {
        set_last_error("invalid position_id");
        return GNUBGAPI_E_INVALID_ARGUMENT;
    }

    /* match_id is required (see parse_position_and_cubeinfo for rationale). */
    if (!match_id || !match_id[0]) {
        set_last_error("match_id is required");
        return GNUBGAPI_E_INVALID_ARGUMENT;
    }

    {
        unsigned int anDice[2];
        int fTurn = 0, fResigned = 0, fDoubled = 0, fMove = 0;
        int fCubeOwner = -1, fCrawford = 0, nMatchTo = 0;
        int anScore[2] = {0, 0};
        int nCube = 1, fJacoby = 0;
        gamestate gs;

        if (MatchFromID(anDice, &fTurn, &fResigned, &fDoubled, &fMove, &fCubeOwner,
                        &fCrawford, &nMatchTo, anScore, &nCube, &fJacoby, &gs, match_id) != 0) {
            set_last_error("invalid match_id");
            return GNUBGAPI_E_INVALID_ARGUMENT;
        }

        if (nMatchTo > 0) {
            if (SetCubeInfo(&ci, nCube, fCubeOwner, fMove, nMatchTo, anScore, fCrawford, fJacoby, 0,
                            VARIATION_STANDARD) != 0) {
                set_last_error("failed to set cube info (match)");
                return GNUBGAPI_E_INTERNAL;
            }
        } else {
            if (SetCubeInfoMoney(&ci, nCube, fCubeOwner, fMove, fJacoby, 0, VARIATION_STANDARD) != 0) {
                set_last_error("failed to set cube info (money)");
                return GNUBGAPI_E_INTERNAL;
            }
        }
    }

    /*
     * Build rollout context from settings and install it in the global
     * rcRollout, because RolloutGeneral() reads the global directly.
     * Save and restore the original to be safe for concurrent callers.
     */
    rolloutcontext rcSave = rcRollout;
    build_rollout_context(&rcRollout, settings);

    float arOutput[NUM_ROLLOUT_OUTPUTS];
    float arStdDev[NUM_ROLLOUT_OUTPUTS];
    rolloutstat arsStatistics[2];
    memset(arsStatistics, 0, sizeof(arsStatistics));

    int rc = GeneralEvaluationR(arOutput, arStdDev, arsStatistics,
                                (ConstTanBoard)board, &ci, &rcRollout,
                                NULL, NULL);
    rcRollout = rcSave; /* restore */

    if (rc != 0) {
        char errmsg[256];
        snprintf(errmsg, sizeof(errmsg), "rollout failed (rc=%d)", rc);
        set_last_error(errmsg);
        return GNUBGAPI_E_INTERNAL;
    }

    for (int i = 0; i < GNUBGAPI_NUM_ROLLOUT_OUTPUTS; i++) {
        out_output[i]  = (double)arOutput[i];
        out_std_dev[i] = (double)arStdDev[i];
    }

    set_last_error("");
    return GNUBGAPI_OK;
}

/* ------------------------------------------------------------------ */
/* Move generation API                                                */
/* ------------------------------------------------------------------ */

/*
 * Helper: fill a gnubgapi_move from a GnuBG internal move struct.
 * Computes the resulting position_id with sides swapped (next player
 * becomes anBoard[1], ready for their evaluation).
 */
static void fill_move_result(gnubgapi_move *out, const move *m) {
    memcpy(out->an_move, m->anMove, sizeof(int) * 8);
    out->n_submoves = m->cMoves;
    out->pips = m->cPips;

    /* Decode the position key stored by GenerateMoves/SaveMoves.
     * The key represents the board after the move, with the mover
     * still in anBoard[1].  SwapSides puts the opponent (next to
     * move) into anBoard[1]. */
    TanBoard resultBoard;
    PositionFromKey(resultBoard, &m->key);
    SwapSides(resultBoard);

    char *pid = PositionID((ConstTanBoard)resultBoard);
    strncpy(out->result_position_id, pid, 15);
    out->result_position_id[14] = '\0';
}

gnubgapi_status gnubgapi_generate_moves(
    gnubgapi_context *ctx,
    const char *position_id,
    int die1, int die2,
    gnubgapi_move *out_moves,
    uint32_t *out_count
) {
    if (!ctx || !position_id || !out_moves || !out_count) {
        set_last_error("null argument");
        return GNUBGAPI_E_INVALID_ARGUMENT;
    }
    if (!ctx->initialized) {
        set_last_error("not initialized");
        return GNUBGAPI_E_NOT_INITIALIZED;
    }
    if (die1 < 1 || die1 > 6 || die2 < 1 || die2 > 6) {
        set_last_error("dice values must be 1-6");
        return GNUBGAPI_E_INVALID_ARGUMENT;
    }

    TanBoard board;
    if (!PositionFromID(board, position_id)) {
        set_last_error("invalid position_id");
        return GNUBGAPI_E_INVALID_ARGUMENT;
    }

    movelist ml;
    GenerateMoves(&ml, (ConstTanBoard)board, die1, die2, FALSE);

    *out_count = ml.cMoves;
    for (unsigned int i = 0; i < ml.cMoves; i++) {
        fill_move_result(&out_moves[i], &ml.amMoves[i]);
    }

    set_last_error("");
    return GNUBGAPI_OK;
}

gnubgapi_status gnubgapi_apply_move(
    gnubgapi_context *ctx,
    const char *position_id,
    const int an_move[8],
    char *out_position_id
) {
    if (!ctx || !position_id || !an_move || !out_position_id) {
        set_last_error("null argument");
        return GNUBGAPI_E_INVALID_ARGUMENT;
    }
    if (!ctx->initialized) {
        set_last_error("not initialized");
        return GNUBGAPI_E_NOT_INITIALIZED;
    }

    TanBoard board;
    if (!PositionFromID(board, position_id)) {
        set_last_error("invalid position_id");
        return GNUBGAPI_E_INVALID_ARGUMENT;
    }

    if (ApplyMove(board, an_move, TRUE) != 0) {
        set_last_error("illegal move");
        return GNUBGAPI_E_INVALID_ARGUMENT;
    }

    SwapSides(board);

    char *pid = PositionID((ConstTanBoard)board);
    strncpy(out_position_id, pid, 15);
    out_position_id[14] = '\0';

    set_last_error("");
    return GNUBGAPI_OK;
}

gnubgapi_status gnubgapi_find_best_move(
    gnubgapi_context *ctx,
    const char *position_id,
    const char *match_id,
    int die1, int die2,
    uint32_t n_plies,
    gnubgapi_move *out_move
) {
    if (!ctx || !position_id || !out_move) {
        set_last_error("null argument");
        return GNUBGAPI_E_INVALID_ARGUMENT;
    }
    if (die1 < 1 || die1 > 6 || die2 < 1 || die2 > 6) {
        set_last_error("dice values must be 1-6");
        return GNUBGAPI_E_INVALID_ARGUMENT;
    }

    TanBoard board;
    cubeinfo ci;
    gnubgapi_status st = parse_position_and_cubeinfo(ctx, position_id, match_id, board, &ci);
    if (st != GNUBGAPI_OK) return st;

    evalcontext ec = {
        .fCubeful = TRUE,
        .nPlies = (unsigned int)n_plies,
        .fUsePrune = (n_plies >= 2) ? TRUE : FALSE,
        .fDeterministic = TRUE,
        .rNoise = 0.0f
    };

    int anMove[8];
    if (FindBestMove(anMove, die1, die2, board, &ci, &ec, defaultFilters) < 0) {
        set_last_error("FindBestMove failed");
        return GNUBGAPI_E_INTERNAL;
    }

    memcpy(out_move->an_move, anMove, sizeof(int) * 8);

    /* Count submoves and pips */
    out_move->n_submoves = 0;
    out_move->pips = 0;
    for (int i = 0; i < 8; i += 2) {
        if (anMove[i] < 0) break;
        out_move->n_submoves++;
        out_move->pips += anMove[i] - anMove[i + 1];
    }

    /* FindBestMove applies the best move to board in-place */
    SwapSides(board);
    char *pid = PositionID((ConstTanBoard)board);
    strncpy(out_move->result_position_id, pid, 15);
    out_move->result_position_id[14] = '\0';

    set_last_error("");
    return GNUBGAPI_OK;
}

gnubgapi_status gnubgapi_generate_moves_with_eval(
    gnubgapi_context *ctx,
    const char *position_id,
    const char *match_id,
    int die1, int die2,
    uint32_t n_plies,
    gnubgapi_scored_move *out_moves,
    uint32_t *out_count
) {
    if (!ctx || !position_id || !out_moves || !out_count) {
        set_last_error("null argument");
        return GNUBGAPI_E_INVALID_ARGUMENT;
    }
    if (die1 < 1 || die1 > 6 || die2 < 1 || die2 > 6) {
        set_last_error("dice values must be 1-6");
        return GNUBGAPI_E_INVALID_ARGUMENT;
    }

    TanBoard board;
    cubeinfo ci;
    gnubgapi_status st = parse_position_and_cubeinfo(ctx, position_id, match_id, board, &ci);
    if (st != GNUBGAPI_OK) return st;

    /* Analysis API: evaluate every legal move at the requested ply with
     * the cube in play (money mode is just match_id with nMatchTo=0).
     * This is the Review / commentary entry point, not a playing-engine
     * hot path — every move must come back with a real cubeful equity,
     * not a sea of -1s.
     *
     * Implementation: gnubg's FindnSaveBestMoves runs an iterative-
     * deepening loop and, at each ply boundary, truncates the move list
     * to the filter's `Accept` count. The truncated moves never get a
     * deeper-ply eval and their arEvalMove[OUTPUT_CUBEFUL_EQUITY] is
     * left at the prune-net's placeholder value (-1.0).
     *
     * Fix: keep gnubg's blessed inner-recursion path (fUsePrune = TRUE,
     * which routes EvaluatePositionCubeful4's opponent-move loop through
     * the prune-net-tested FindBestMoveInEval — that's the path
     * production gnubg has used for 20+ years) but pass a custom
     * non-truncating filter so the OUTER iterative-deepening loop never
     * drops a move. Every legal move ends up scored at the requested
     * final ply. Earlier attempt (fUsePrune = FALSE) regressed by
     * pushing the inner recursion onto FindBestMovePlied, an alternate
     * path that doesn't fully populate arEvalMove for our use case.
     */
    evalcontext ec = {
        .fCubeful = TRUE,
        .nPlies = (unsigned int)n_plies,
        .fUsePrune = TRUE,
        .fDeterministic = TRUE,
        .rNoise = 0.0f
    };

    /* Non-truncating filter: at every ply boundary, accept every move
     * that's still in the list. MAX_MOVES (3060) is the upper bound on
     * the number of legal-move sequences for any one dice roll, so
     * MIN(Accept, cMoves) inside FindnSaveBestMoves never truncates.
     * Avoid INT_MAX here — gnubg arithmetic on Accept may overflow. */
    movefilter no_truncate[MAX_FILTER_PLIES][MAX_FILTER_PLIES];
    memset(no_truncate, 0, sizeof(no_truncate));
    for (int i = 0; i < MAX_FILTER_PLIES; i++)
        for (int j = 0; j < MAX_FILTER_PLIES; j++)
            no_truncate[i][j].Accept = MAX_MOVES;

    movelist ml;
    memset(&ml, 0, sizeof(ml));

    if (FindnSaveBestMoves(&ml, die1, die2, (ConstTanBoard)board,
                           NULL, FALSE, 0.0f, &ci, &ec, no_truncate) < 0) {
        set_last_error("FindnSaveBestMoves failed");
        return GNUBGAPI_E_INTERNAL;
    }

    *out_count = ml.cMoves;
    for (unsigned int i = 0; i < ml.cMoves; i++) {
        fill_move_result(&out_moves[i].move, &ml.amMoves[i]);
        out_moves[i].equity = (double)ml.amMoves[i].arEvalMove[OUTPUT_CUBEFUL_EQUITY];
        for (int j = 0; j < 5; j++) {
            out_moves[i].probs[j] = (double)ml.amMoves[i].arEvalMove[j];
        }
    }

    /* FindnSaveBestMoves allocates amMoves with g_memdup — must free */
    if (ml.amMoves) {
        g_free(ml.amMoves);
    }

    set_last_error("");
    return GNUBGAPI_OK;
}

/* ------------------------------------------------------------------ */
/* Game analysis API                                                  */
/* ------------------------------------------------------------------ */

/*
 * Skill thresholds and rating tables — copied from gnubg.c/analysis.c
 * to keep the API self-contained without linking analysis.o.
 */
static const float arSkillLevel_[] = {
    0.12f,   /* SKILL_VERYBAD (blunder) */
    0.06f,   /* SKILL_BAD */
    0.03f,   /* SKILL_DOUBTFUL */
    0.0f     /* SKILL_NONE */
};

static int skill_classify(float r) {
    if (r < -arSkillLevel_[0]) return 0;  /* VERYBAD */
    if (r < -arSkillLevel_[1]) return 1;  /* BAD */
    if (r < -arSkillLevel_[2]) return 2;  /* DOUBTFUL */
    return 3;                              /* NONE */
}

static const float arThrsRating_[] = {
    1e38f,    /* BEGINNER       (>= 0.032) */
    0.032f,   /* INTERMEDIATE   (< 0.032) */
    0.020f,   /* ADVANCED       (< 0.020) */
    0.013f,   /* MASTER         (< 0.013) */
    0.008f,   /* GRANDMASTER    (< 0.008) */
    0.005f    /* SUPERGRANDMASTER (< 0.005) */
};

static const char *aszRating_[] = {
    "Beginner", "Intermediate", "Advanced",
    "Master", "Grandmaster", "Super Grandmaster"
};

static const char *get_rating(float rError) {
    for (int i = 5; i >= 0; i--)
        if (rError < arThrsRating_[i])
            return aszRating_[i];
    return aszRating_[0];
}

gnubgapi_status gnubgapi_analyse_game(
    gnubgapi_context *ctx,
    const gnubgapi_game_turn *turns,
    uint32_t num_turns,
    uint32_t n_plies,
    gnubgapi_analysis_result *out
) {
    if (!ctx || !turns || !out) {
        set_last_error("null argument");
        return GNUBGAPI_E_INVALID_ARGUMENT;
    }
    if (!ctx->initialized) {
        set_last_error("not initialized");
        return GNUBGAPI_E_NOT_INITIALIZED;
    }

    memset(out, 0, sizeof(*out));
    out->n_games = 1;

    /* Start from the standard opening position. */
    TanBoard board;
    init_standard_board(board);

    /* Money-game cube info — no cube decisions in self-play. */
    cubeinfo ci;
    SetCubeInfoMoney(&ci, 1, -1, 0, 0, 0, VARIATION_STANDARD);

    evalcontext ec = {
        .fCubeful = TRUE,
        .nPlies = (unsigned int)n_plies,
        .fUsePrune = (n_plies >= 2) ? TRUE : FALSE,
        .fDeterministic = TRUE,
        .rNoise = 0.0f
    };

    for (uint32_t t = 0; t < num_turns; t++) {
        const gnubgapi_game_turn *turn = &turns[t];
        int player = turn->player;

        if (turn->die1 < 1 || turn->die1 > 6 ||
            turn->die2 < 1 || turn->die2 > 6)
            continue;

        /* Generate all legal moves for this position + dice. */
        movelist ml;
        memset(&ml, 0, sizeof(ml));
        GenerateMoves(&ml, (ConstTanBoard)board, turn->die1, turn->die2, FALSE);

        if (ml.cMoves == 0) {
            /* No legal moves — forced pass, swap sides. */
            SwapSides(board);
            continue;
        }

        out->total_moves[player]++;

        if (ml.cMoves == 1) {
            /* Forced move — no decision to analyse. */
            out->skill_counts[player][SKILL_NONE]++;
            ApplyMove(board, ml.amMoves[0].anMove, FALSE);
            SwapSides(board);
            continue;
        }

        /* Multiple legal moves — this is a decision point. */
        out->unforced_moves[player]++;

        /* Evaluate all candidates with FindnSaveBestMoves (sorted best-first). */
        movelist ranked;
        memset(&ranked, 0, sizeof(ranked));

        ci.fMove = player;

        if (FindnSaveBestMoves(&ranked, turn->die1, turn->die2,
                                (ConstTanBoard)board, NULL, FALSE,
                                0.0f, &ci, &ec, defaultFilters) < 0) {
            set_last_error("FindnSaveBestMoves failed");
            return GNUBGAPI_E_INTERNAL;
        }

        /* Find the played move by comparing result position keys. */
        TanBoard afterBoard;
        memcpy(afterBoard, board, sizeof(TanBoard));
        ApplyMove(afterBoard, turn->an_move, FALSE);
        positionkey afterKey;
        PositionKey((ConstTanBoard)afterBoard, &afterKey);

        int played_idx = -1;
        for (unsigned int i = 0; i < ranked.cMoves; i++) {
            if (EqualKeys(afterKey, ranked.amMoves[i].key)) {
                played_idx = (int)i;
                break;
            }
        }

        /* Compute skill = played.rScore - best.rScore (negative = error). */
        float rSkill = 0.0f;
        if (played_idx >= 0 && ranked.cMoves > 0) {
            rSkill = ranked.amMoves[played_idx].rScore -
                     ranked.amMoves[0].rScore;
        }

        int st = skill_classify(rSkill);
        out->skill_counts[player][st]++;

        /* Accumulate error (rSkill is negative when move is worse). */
        if (rSkill < 0.0f)
            out->total_error[player] -= rSkill;

        if (ranked.amMoves)
            g_free(ranked.amMoves);

        /* Apply the played move and swap sides. */
        ApplyMove(board, turn->an_move, FALSE);
        SwapSides(board);
    }

    /* Compute derived statistics. */
    for (int p = 0; p < 2; p++) {
        if (out->unforced_moves[p] > 0)
            out->error_per_move[p] =
                out->total_error[p] / (float)out->unforced_moves[p];

        out->mpr[p] = out->error_per_move[p] * 1000.0f;

        const char *rs = get_rating(out->error_per_move[p]);
        strncpy(out->rating[p], rs, 31);
        out->rating[p][31] = '\0';
    }

    set_last_error("");
    return GNUBGAPI_OK;
}

/* ------------------------------------------------------------------ */
/* .mat file parser + analysis                                        */
/* ------------------------------------------------------------------ */

/*
 * Parse a Jellyfish .mat move notation token (e.g. "13/7", "bar/22",
 * "6/off") into a from-to pair.  Returns 1 on success.
 */
static int parse_mat_submove(const char *tok, int *from, int *to) {
    /* Handle "bar/N" */
    if (strncmp(tok, "bar/", 4) == 0) {
        *from = 24;  /* bar */
        *to = atoi(tok + 4) - 1;
        return (*to >= 0 && *to < 24) ? 1 : 0;
    }

    /* Handle "N/off" */
    const char *slash = strchr(tok, '/');
    if (!slash) return 0;

    *from = atoi(tok) - 1;  /* 1-indexed → 0-indexed */
    if (*from < 0 || *from >= 24) return 0;

    if (strncmp(slash + 1, "off", 3) == 0) {
        /* Bear off: GnuBG uses negative dest to signal off. We use
         * the convention dest = -1 for "off the board". */
        *to = -1;
        return 1;
    }

    *to = atoi(slash + 1) - 1;
    if (*to == -1) return 1;  /* "N/0" also means bear-off */
    return (*to >= 0 && *to < 24) ? 1 : 0;
}

/*
 * Parse a full move string like "13/7 6/3" or "bar/22 13/7(2)"
 * into an an_move[8] array.  Returns the number of submoves parsed.
 */
static int parse_mat_move(const char *move_str, int an_move[8]) {
    for (int i = 0; i < 8; i++) an_move[i] = -1;

    int idx = 0;
    const char *p = move_str;

    while (*p && idx < 8) {
        /* Skip whitespace. */
        while (*p == ' ' || *p == '\t') p++;
        if (!*p) break;

        /* Read one submove token (until space, '(' or end). */
        char tok[32];
        int ti = 0;
        while (*p && *p != ' ' && *p != '\t' && *p != '(' && ti < 30)
            tok[ti++] = *p++;
        tok[ti] = '\0';

        if (ti == 0) break;

        int from, to;
        if (!parse_mat_submove(tok, &from, &to))
            break;

        /* Check for "(N)" repetition. */
        int reps = 1;
        if (*p == '(') {
            p++;
            reps = atoi(p);
            while (*p && *p != ')') p++;
            if (*p == ')') p++;
            if (reps < 1 || reps > 4) reps = 1;
        }

        for (int r = 0; r < reps && idx < 8; r++) {
            an_move[idx++] = from;
            an_move[idx++] = to;
        }
    }

    return idx / 2;
}

/*
 * Parse a Jellyfish .mat file and extract turns.
 * Allocates *out_turns via malloc; caller must free.
 * Returns 0 on success, -1 on error.
 */
static int parse_mat_file(
    const char *mat_path,
    gnubgapi_game_turn **out_turns,
    uint32_t *out_num_turns
) {
    FILE *fp = fopen(mat_path, "r");
    if (!fp) return -1;

    /* Pre-allocate space for turns (grow if needed). */
    uint32_t cap = 256;
    uint32_t count = 0;
    gnubgapi_game_turn *turns = (gnubgapi_game_turn *)malloc(
        cap * sizeof(gnubgapi_game_turn));
    if (!turns) { fclose(fp); return -1; }

    char line[1024];
    int in_game = 0;

    while (fgets(line, sizeof(line), fp)) {
        /* Strip trailing newline / CR. */
        size_t len = strlen(line);
        while (len > 0 && (line[len-1] == '\n' || line[len-1] == '\r'))
            line[--len] = '\0';

        /* Skip empty lines and comments. */
        if (len == 0 || line[0] == ';') continue;

        /* Detect "Game N" or " Game N" header. */
        {
            const char *g = line;
            while (*g == ' ' || *g == '\t') g++;
            if (strncmp(g, "Game ", 5) == 0) {
                in_game = 1;
                continue;
            }
        }

        /* Skip non-game lines (e.g. "Wins N point"). */
        if (!in_game) continue;

        /* Parse move line:  "  N) d1d2: move1   d1d2: move2" */
        const char *p = line;
        while (*p == ' ' || *p == '\t') p++;

        /* Check for "N)" prefix. */
        if (!(*p >= '0' && *p <= '9')) continue;
        while (*p >= '0' && *p <= '9') p++;
        if (*p != ')') continue;
        p++;  /* skip ')' */
        while (*p == ' ' || *p == '\t') p++;

        /* Parse up to two half-moves on this line. */
        for (int half = 0; half < 2; half++) {
            while (*p == ' ' || *p == '\t') p++;
            if (!*p) break;

            /* Read dice: two digits (e.g. "31" or "66"). */
            if (!(p[0] >= '1' && p[0] <= '6') ||
                !(p[1] >= '1' && p[1] <= '6'))
                break;

            int die1 = p[0] - '0';
            int die2 = p[1] - '0';
            p += 2;

            /* Skip ": " */
            if (*p == ':') p++;
            while (*p == ' ' || *p == '\t') p++;

            /* Determine how much of the remaining string is this
             * player's move text.  For half=0, the second player's
             * move starts at a column with a digit pair followed by ':'.
             * Simple heuristic: scan for the next "  D1D2:" pattern. */
            const char *end = NULL;
            if (half == 0) {
                /* Look for next dice pattern (2+ spaces then digit-digit-colon). */
                for (const char *q = p; *q; q++) {
                    if ((q == p || *(q-1) == ' ' || *(q-1) == '\t') &&
                        q[0] >= '1' && q[0] <= '6' &&
                        q[1] >= '1' && q[1] <= '6' &&
                        q[2] == ':') {
                        end = q;
                        break;
                    }
                }
            }

            /* Extract the move text. */
            char move_text[256];
            if (end) {
                size_t mlen = (size_t)(end - p);
                if (mlen > 255) mlen = 255;
                memcpy(move_text, p, mlen);
                move_text[mlen] = '\0';
                p = end;
            } else {
                strncpy(move_text, p, 255);
                move_text[255] = '\0';
                p += strlen(p);
            }

            /* Trim trailing whitespace. */
            size_t mlen = strlen(move_text);
            while (mlen > 0 && (move_text[mlen-1] == ' ' ||
                                move_text[mlen-1] == '\t'))
                move_text[--mlen] = '\0';

            /* Grow turns array if needed. */
            if (count >= cap) {
                cap *= 2;
                gnubgapi_game_turn *tmp = (gnubgapi_game_turn *)realloc(
                    turns, cap * sizeof(gnubgapi_game_turn));
                if (!tmp) { free(turns); fclose(fp); return -1; }
                turns = tmp;
            }

            gnubgapi_game_turn *turn = &turns[count];
            turn->player = half;
            turn->die1 = die1;
            turn->die2 = die2;

            /* Handle "Cannot Move" or empty moves. */
            if (strstr(move_text, "Cannot") || mlen == 0) {
                for (int i = 0; i < 8; i++) turn->an_move[i] = -1;
            } else {
                parse_mat_move(move_text, turn->an_move);
            }

            count++;
        }
    }

    fclose(fp);
    *out_turns = turns;
    *out_num_turns = count;
    return 0;
}

gnubgapi_status gnubgapi_analyse_mat(
    gnubgapi_context *ctx,
    const char *mat_path,
    uint32_t n_plies,
    gnubgapi_analysis_result *out
) {
    if (!ctx || !mat_path || !out) {
        set_last_error("null argument");
        return GNUBGAPI_E_INVALID_ARGUMENT;
    }
    if (!ctx->initialized) {
        set_last_error("not initialized");
        return GNUBGAPI_E_NOT_INITIALIZED;
    }

    gnubgapi_game_turn *turns = NULL;
    uint32_t num_turns = 0;

    if (parse_mat_file(mat_path, &turns, &num_turns) != 0) {
        set_last_error("failed to parse .mat file");
        return GNUBGAPI_E_INTERNAL;
    }

    if (num_turns == 0) {
        free(turns);
        set_last_error("no turns found in .mat file");
        return GNUBGAPI_E_INVALID_ARGUMENT;
    }

    gnubgapi_status st = gnubgapi_analyse_game(ctx, turns, num_turns,
                                                n_plies, out);
    free(turns);
    return st;
}

/* ------------------------------------------------------------------ */
/* Feature encoding API                                               */
/* ------------------------------------------------------------------ */

/*
 * Precomputed escape table: for each 12-bit blocking pattern, how many
 * of the 36 ordered dice rolls (d1,d2) allow at least one legal move.
 * Bit i of the pattern means point (i+1) ahead is blocked (2+ opp).
 */
static int g_escape_table[4096];
static int g_escape_table_ready = 0;

static void ensure_escape_table(void) {
    if (g_escape_table_ready) return;
    for (int pattern = 0; pattern < 4096; pattern++) {
        int count = 0;
        for (int d1 = 1; d1 <= 6; d1++) {
            for (int d2 = 1; d2 <= 6; d2++) {
                if (d1 == d2) {
                    if (!((pattern >> (d1 - 1)) & 1))
                        count++;
                } else {
                    int b1 = (pattern >> (d1 - 1)) & 1;
                    int b2 = (pattern >> (d2 - 1)) & 1;
                    if (!b1 || !b2)
                        count++;
                }
            }
        }
        g_escape_table[pattern] = count;
    }
    g_escape_table_ready = 1;
}

/*
 * Return number of rolls (0-36) that let a checker escape from
 * position n on the opponent's board.
 * opp is a 25-element array (opponent's perspective).
 */
static int escapes_count(const unsigned int opp[25], int n) {
    int pattern = 0;
    for (int i = 0; i < 12; i++) {
        int pt = n - i - 1;
        if (pt >= 0 && pt < 24 && opp[pt] >= 2)
            pattern |= 1 << i;
    }
    return g_escape_table[pattern];
}

/*
 * gnubg's real per-side contact/crashed inputs (25 per side), assembled from
 * gnubg's own functions (un-static'd in eval.c; logic untouched) so the API
 * emits exactly the inputs gnubg's net consumes, with no reimplemented feature
 * math in this wrapper:
 *
 *   out[0..2]  = men-off triple, via menOffAll (full 0..15-off range, no
 *                contact-domain assert). This is gnubg's CRASHED-net men-off
 *                encoding -- the only one valid across every position class, so
 *                a single student net can consume it everywhere (contact,
 *                crashed, backgame, race, bearoff). gnubg's contact net uses
 *                menOffNonCrashed instead, but that asserts menOff<=8 and so
 *                cannot serve a single net that also sees races/bearoffs.
 *   out[3..24] = I_BREAK_CONTACT .. I_BACKRESCAPES, via CalculateHalfInputs
 *                (identical function for gnubg's contact and crashed nets).
 *
 * baseInputs + menOffAll + CalculateHalfInputs is exactly gnubg's
 * CalculateCrashedInputs (eval.c), re-laid-out per side by
 * gnubgapi_position_to_features.
 */
extern void CalculateHalfInputs(const unsigned int anBoard[25],
                                const unsigned int anBoardOpp[25], float afInput[]);
extern void menOffAll(const unsigned int *anBoard, float *afInput);

static void contact_features_25(
    const unsigned int own[25],
    const unsigned int opp[25],
    float out[25]
) {
    menOffAll(own, out);                 /* out[0..2]  = I_OFF1/2/3 (full 0..15 range) */
    CalculateHalfInputs(own, opp, out);  /* out[3..24] = I_BREAK_CONTACT .. I_BACKRESCAPES */
}

gnubgapi_status gnubgapi_position_to_features(
    gnubgapi_context *ctx,
    const char *position_id,
    int is_top_on_roll,
    float *out_features
) {
    if (!ctx || !position_id || !out_features) {
        set_last_error("null argument");
        return GNUBGAPI_E_INVALID_ARGUMENT;
    }
    if (!ctx->initialized) {
        set_last_error("not initialized");
        return GNUBGAPI_E_NOT_INITIALIZED;
    }

    ensure_escape_table();

    TanBoard anBoard;
    if (!PositionFromID(anBoard, position_id)) {
        set_last_error("invalid position_id");
        return GNUBGAPI_E_INVALID_ARGUMENT;
    }

    /*
     * anBoard[0] = opponent (encoded first in position_id)
     * anBoard[1] = on-roll player
     *
     * We need bottom[25] and top[25] each from their own perspective
     * (index 0 = that side's 1-point), then build features as:
     *   [bottom_base(100), bottom_contact(25),
     *    top_base(100),    top_contact(25)]
     *
     * Python's _to_gnubg_boards() returns each side from their own
     * perspective.  Top's 1-point is our board index 23, so top's
     * board is our bottom_points reversed.
     */
    unsigned int bottom[25];
    unsigned int top[25];

    if (!is_top_on_roll) {
        /* Bottom is on roll: anBoard[1] = bottom (own perspective),
         * anBoard[0] = top (own perspective).  No reversal needed —
         * both are already from their own side's viewpoint. */
        memcpy(bottom, anBoard[1], 25 * sizeof(unsigned int));
        memcpy(top, anBoard[0], 25 * sizeof(unsigned int));
    } else {
        /* Top is on roll: anBoard[1] = top (own perspective),
         * anBoard[0] = bottom (own perspective). */
        memcpy(bottom, anBoard[0], 25 * sizeof(unsigned int));
        memcpy(top, anBoard[1], 25 * sizeof(unsigned int));
    }

    /*
     * baseInputs() processes anBoard[0] first (→ features[0:100]),
     * then anBoard[1] (→ features[100:200]).
     * We want bottom first, then top.
     */
    TanBoard featureBoard;
    memcpy(featureBoard[0], bottom, 25 * sizeof(unsigned int));
    memcpy(featureBoard[1], top, 25 * sizeof(unsigned int));

    /* Base features: [0:100] = bottom, [100:200] = top. */
    baseInputs((ConstTanBoard)featureBoard, out_features);

    /* Contact features: 25 per side = gnubg's real menOffAll + CalculateHalfInputs.
     * baseInputs gave [bottom_base(100), top_base(100)]; shift top_base to [125:225],
     * then fill [bottom_base(100), bottom_contact(25), top_base(100), top_contact(25)] = 250.
     */
    memmove(&out_features[125], &out_features[100], 100 * sizeof(float));

    /* Bottom contact -> [100:125]. */
    contact_features_25(bottom, top, &out_features[100]);

    /* Top contact -> [225:250]. */
    contact_features_25(top, bottom, &out_features[225]);

    set_last_error("");
    return GNUBGAPI_OK;
}
